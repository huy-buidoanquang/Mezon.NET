#!/usr/bin/env python3
"""Generate protobuf boundary layer: *Params, *Response view structs, and API facades."""

from __future__ import annotations

import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

ROOT = Path(__file__).resolve().parent.parent
CLIENT = ROOT / "src" / "Mezon.Net.Client"
API_INTERFACE = CLIENT / "Abstractions" / "IMezonApiClient.cs"
SOCKET_CLIENT = CLIENT / "Clients" / "MezonSocketClient.cs"
EVENTS_FILE = CLIENT / "BaseMezonSocketClient.Events.cs"
API_PROTO = ROOT / "src" / "Mezon.Net.Core" / "Protobuf" / "Api" / "api.proto"
REALTIME_PROTO = ROOT / "src" / "Mezon.Net.Core" / "Protobuf" / "Realtime" / "realtime.proto"
MEZON_JS_TRANSPORT = ROOT.parent / "mezon-js" / "packages" / "mezon-js" / "transport.ts"

OUT_RESPONSES = CLIENT / "Models" / "Responses"
OUT_REQUESTS = CLIENT / "Models" / "Requests"
OUT_EVENTS = CLIENT / "Models" / "Responses" / "Events"
OUT_GENERATED = CLIENT / "Generated"

NS_MODELS = "Mezon.Net.Models"
NS_ABSTRACTIONS = "Mezon.Net.Abstractions"
NS_MAPPER = "Mezon.Net.Client.Models.Internal"
NS_CLIENT = "Mezon.Net.Client"

api_messages: Dict[str, ProtoMessage] = {}
rt_messages: Dict[str, ProtoMessage] = {}
api_enums: Dict[str, str] = {}
rt_enums: Dict[str, str] = {}

API_NS = "Mezon.Net.Internal.Api"
RT_NS = "Mezon.Net.Internal.Realtime"

SKIP_METHODS = {
    "LoginAsync",
    "LogoutAsync",
    "SendNoResAsync",
    "SendJsonNoResAsync",
    "SendMultipartNoResAsync",
    "SendAsync",
    "SendJsonAsync",
    "SendMultipartAsync",
    "ConfigureGatewayBasePath",
    "ConfigureApiBasePath",
}

SKIP_METHOD_RULES = [
    ("UpdateAccountAsync", lambda m: "UpdateAccountRequest body" in m.args_raw and "Internal.Api" not in m.args_raw),
    ("SendChannelMessageAsync", lambda m: "ChannelMessageSend body" in m.args_raw),
]


def dedupe_methods(methods: List[ApiMethod]) -> List[ApiMethod]:
    """Keep one method per name; prefer signatures that include RequestOptions."""
    by_name: Dict[str, ApiMethod] = {}
    for m in methods:
        existing = by_name.get(m.name)
        if existing is None:
            by_name[m.name] = m
            continue
        has_opts = any(n == "options" for _, n in m.args)
        existing_has_opts = any(n == "options" for _, n in existing.args)
        if has_opts and not existing_has_opts:
            by_name[m.name] = m
    return list(by_name.values())

PRIMITIVE_CS = {
    "string",
    "int",
    "long",
    "uint",
    "ulong",
    "bool",
    "float",
    "double",
    "byte",
    "object",
}

WRAPPER_MAP = {
    "google.protobuf.StringValue": ("string", True),
    "google.protobuf.Int32Value": ("int", True),
    "google.protobuf.Int64Value": ("long", True),
    "google.protobuf.UInt32Value": ("uint", True),
    "google.protobuf.UInt64Value": ("ulong", True),
    "google.protobuf.BoolValue": ("bool", True),
    "google.protobuf.FloatValue": ("float", True),
    "google.protobuf.DoubleValue": ("double", True),
    "google.protobuf.BytesValue": ("byte[]", True),
}

CUSTOM_BODY_TO_PROTO = {
    "EmailAuthenticationRequest": None,
    "AppAuthenticationRequest": None,
    "AuthenticateSMSRequest": None,
    "SendChannelMessageParams": "ChannelMessageSend",
}

SKIP_REQUEST_PROTO = {
    "ChannelMessageSend",
    "EphemeralMessageSend",
    "QuickMenuDataEvent",
}

HAND_WRITTEN_PARAMS = {
    "SendChannelMessageParams",
    "ReplyMessageParams",
    "UpdateMessageParams",
    "DeleteMessageParams",
    "ReactMessageParams",
    "SendEphemeralMessageParams",
    "QuickMenuDataEventParams",
}

# Hand-written response facades (decode nested bytes, custom caching, etc.).
HAND_WRITTEN_RESPONSES = {
    "ChannelMessage",
    "ChannelMessageUpdate",
}


@dataclass
class RealtimeMethod:
    js_name: str
    dotnet_name: str
    envelope_property: str
    request_proto: Optional[str]
    params_type: Optional[str]
    param_name: str
    body_expr: str
    response_property: Optional[str] = None
    response_data_type: Optional[str] = None
    build_full_envelope: bool = False
    no_params: bool = False


REALTIME_METHODS: List[RealtimeMethod] = [
    RealtimeMethod("followUsers", "FollowUsersRtAsync", "StatusFollow", "StatusFollow", "StatusFollowParams", "body", "{Mapper}.ToProto(body)"),
    RealtimeMethod("joinClanChat", "JoinClanChatRtAsync", "ClanJoin", "ClanJoin", "ClanJoinParams", "body", "{Mapper}.ToProto(body)"),
    RealtimeMethod("follower", "FollowerRtAsync", "FollowEvent", "FollowEvent", None, "", "new global::Mezon.Net.Internal.Realtime.FollowEvent()", no_params=True),
    RealtimeMethod("joinChat", "JoinChannelChatRtAsync", "ChannelJoin", "ChannelJoin", "ChannelJoinParams", "body", "{Mapper}.ToProto(body)"),
    RealtimeMethod("leaveChat", "LeaveChannelChatRtAsync", "ChannelLeave", "ChannelLeave", "ChannelLeaveParams", "body", "{Mapper}.ToProto(body)"),
    RealtimeMethod(
        "removeChatMessage",
        "RemoveChatMessageRtAsync",
        "ChannelMessageRemove",
        "ChannelMessageRemove",
        "DeleteMessageParams",
        "message",
        "Mezon.Net.Client.Messaging.MessageSendHelper.ToChannelMessageRemove(message)",
        response_property="ChannelMessageAck",
        response_data_type="ChannelMessageAckResponse",
    ),
    RealtimeMethod("unfollowUsers", "UnfollowUsersRtAsync", "StatusUnfollow", "StatusUnfollow", "StatusUnfollowParams", "body", "{Mapper}.ToProto(body)"),
    RealtimeMethod(
        "updateChatMessage",
        "UpdateChatMessageRtAsync",
        "ChannelMessageUpdate",
        "ChannelMessageUpdate",
        "UpdateMessageParams",
        "message",
        "Mezon.Net.Client.Messaging.MessageSendHelper.ToChannelMessageUpdate(message)",
        response_property="ChannelMessageAck",
        response_data_type="ChannelMessageAckResponse",
    ),
    RealtimeMethod("updateStatus", "UpdateStatusRtAsync", "StatusUpdate", "StatusUpdate", "StatusUpdateParams", "body", "{Mapper}.ToProto(body)"),
    RealtimeMethod(
        "writeQuickMenuEvent",
        "SendQuickMenuEventRtAsync",
        "QuickMenuEvent",
        "QuickMenuDataEvent",
        "QuickMenuDataEventParams",
        "body",
        "Mezon.Net.Client.Messaging.MessageSendHelper.ToQuickMenuEnvelope(body)",
        build_full_envelope=True,
    ),
    RealtimeMethod(
        "writeEphemeralMessage",
        "SendEphemeralMessageRtAsync",
        "EphemeralMessageSend",
        "EphemeralMessageSend",
        "SendEphemeralMessageParams",
        "body",
        "Mezon.Net.Client.Messaging.MessageSendHelper.ToEphemeralEnvelope(body)",
        build_full_envelope=True,
        response_property="ChannelMessageAck",
        response_data_type="ChannelMessageAckResponse",
    ),
    RealtimeMethod(
        "writeChatMessage",
        "SendChatMessageRtAsync",
        "ChannelMessageSend",
        "ChannelMessageSend",
        "SendChannelMessageParams",
        "message",
        "Mezon.Net.Client.Messaging.MessageSendHelper.ToChannelMessageSend(message)",
        response_property="ChannelMessageAck",
        response_data_type="ChannelMessageAckResponse",
    ),
    RealtimeMethod(
        "writeMessageReaction",
        "SendMessageReactionRtAsync",
        "MessageReactionEvent",
        "MessageReaction",
        "ReactMessageParams",
        "message",
        "Mezon.Net.Client.Messaging.MessageSendHelper.ToMessageReaction(message)",
    ),
    RealtimeMethod(
        "writeMessageTyping",
        "SendMessageTypingRtAsync",
        "MessageTypingEvent",
        "MessageTypingEvent",
        "MessageTypingEventParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "writeLastSeenMessage",
        "SendLastSeenMessageRtAsync",
        "LastSeenMessageEvent",
        "LastSeenMessageEvent",
        "LastSeenMessageEventParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "writeLastPinMessage",
        "SendLastPinMessageRtAsync",
        "LastPinMessageEvent",
        "LastPinMessageEvent",
        "LastPinMessageEventParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "writeCustomStatus",
        "SendCustomStatusRtAsync",
        "CustomStatusEvent",
        "CustomStatusEvent",
        "CustomStatusEventParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "writeVoiceReaction",
        "SendVoiceReactionRtAsync",
        "VoiceReactionSend",
        "VoiceReactionSend",
        "VoiceReactionSendParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "forwardWebrtcSignaling",
        "ForwardWebrtcSignalingRtAsync",
        "WebrtcSignalingFwd",
        "WebrtcSignalingFwd",
        "WebrtcSignalingFwdParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "makeCallPush",
        "MakeCallPushRtAsync",
        "IncomingCallPush",
        "IncomingCallPush",
        "IncomingCallPushParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
    RealtimeMethod(
        "writeChannelAppEvent",
        "SendChannelAppEventRtAsync",
        "ChannelAppEvent",
        "ChannelAppEvent",
        "ChannelAppEventParams",
        "body",
        "{Mapper}.ToProto(body)",
    ),
]


@dataclass
class ProtoField:
    name: str
    proto_type: str
    number: int
    repeated: bool = False
    map_key: Optional[str] = None
    map_value: Optional[str] = None


@dataclass
class ProtoMessage:
    name: str
    namespace: str
    parent: Optional[str] = None
    fields: List[ProtoField] = field(default_factory=list)


@dataclass
class ApiMethod:
    name: str
    ret_raw: str
    args_raw: str
    args: List[Tuple[str, str]]  # (type, name)


@dataclass
class SocketEvent:
    name: str
    field_name: str
    payload_type: str
    payload_ns: str


def snake_to_pascal(name: str) -> str:
    parts = name.split("_")
    return "".join(p[:1].upper() + p[1:] for p in parts if p)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def parse_proto_messages(path: Path, namespace: str) -> Tuple[Dict[str, ProtoMessage], Dict[str, str]]:
    text = read_text(path)
    messages: Dict[str, ProtoMessage] = {}
    enums: Dict[str, str] = {}

    for em in re.finditer(r"^enum\s+(\w+)\s*\{", text, re.MULTILINE):
        enums[em.group(1)] = namespace

    field_re = re.compile(
        r"^(repeated\s+)?([\w.]+)\s+(\w+)\s*=\s*(\d+)\s*;",
        re.MULTILINE,
    )
    map_re = re.compile(
        r"^map\s*<\s*([\w.]+)\s*,\s*([\w.]+)\s*>\s+(\w+)\s*=\s*(\d+)\s*;",
        re.MULTILINE,
    )
    nested_msg_re = re.compile(r"^  message\s+(\w+)\s*\{", re.MULTILINE)

    def extract_brace_block(source: str, start: int) -> Tuple[str, int]:
        depth = 1
        i = start
        while i < len(source) and depth > 0:
            if source[i] == "{":
                depth += 1
            elif source[i] == "}":
                depth -= 1
            i += 1
        return source[start : i - 1], i

    def dedent_block(body: str) -> str:
        lines = body.split("\n")
        indents = [len(line) - len(line.lstrip()) for line in lines if line.strip()]
        if not indents:
            return body
        min_indent = min(indents)
        return "\n".join(line[min_indent:] if len(line) >= min_indent else line for line in lines)

    def strip_nested_definitions(body: str) -> str:
        lines = body.split("\n")
        output: List[str] = []
        skip_depth = 0
        for line in lines:
            if skip_depth == 0 and re.match(r"^\s*(message|enum)\s+\w+\s*\{", line):
                skip_depth = 1
                continue
            if skip_depth > 0:
                skip_depth += line.count("{") - line.count("}")
                continue
            output.append(line)
        return "\n".join(output)

    def parse_message_body(name: str, parent: Optional[str], body: str) -> None:
        msg = ProtoMessage(name=name, namespace=namespace, parent=parent)
        field_body = dedent_block(strip_nested_definitions(body))
        for fm in map_re.finditer(field_body):
            msg.fields.append(
                ProtoField(
                    name=fm.group(3),
                    proto_type="map",
                    number=int(fm.group(4)),
                    map_key=fm.group(1),
                    map_value=fm.group(2),
                )
            )
        for fm in field_re.finditer(field_body):
            msg.fields.append(
                ProtoField(
                    name=fm.group(3),
                    proto_type=fm.group(2),
                    number=int(fm.group(4)),
                    repeated=bool(fm.group(1)),
                )
            )
        messages[name] = msg

        for nm in nested_msg_re.finditer(body):
            nested_start = nm.end()
            nested_body, _ = extract_brace_block(body, nested_start)
            parse_message_body(nm.group(1), name, nested_body)

    top_msg_re = re.compile(r"^message\s+(\w+)\s*\{", re.MULTILINE)
    for match in top_msg_re.finditer(text):
        body, _ = extract_brace_block(text, match.end())
        parse_message_body(match.group(1), None, body)

    return messages, enums


def resolve_proto_type(proto_type: str, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> Tuple[str, str]:
    if proto_type.startswith("api."):
        return API_NS, proto_type[4:]
    if proto_type in api_msgs:
        return API_NS, proto_type
    if proto_type in rt_msgs:
        return RT_NS, proto_type
    return API_NS, proto_type


def proto_field_cs_type(
    fld: ProtoField,
    api_msgs: Dict[str, ProtoMessage],
    rt_msgs: Dict[str, ProtoMessage],
    *,
    for_params: bool,
) -> str:
    if fld.proto_type == "map":
        key_cs = scalar_proto_to_cs(fld.map_key or "string", for_params)
        val_ns, val_name = resolve_proto_type(fld.map_value or "string", api_msgs, rt_msgs)
        if val_name in api_msgs or val_name in rt_msgs:
            val_cs = params_type_name(val_name) if for_params else data_type_name(val_name)
        else:
            val_cs = scalar_proto_to_cs(fld.map_value or "string", for_params)
        if for_params:
            return f"IReadOnlyDictionary<{key_cs}, {val_cs}>?"
        return f"IReadOnlyDictionary<{key_cs}, {val_cs}>"

    if fld.repeated:
        inner_fld = ProtoField(fld.name, fld.proto_type, fld.number)
        inner = proto_field_cs_type(inner_fld, api_msgs, rt_msgs, for_params=for_params)
        if for_params:
            return f"IEnumerable<{inner}>?"
        return f"ProtoListView<{inner}>"

    if fld.proto_type == "bytes":
        return "byte[]?" if for_params else "byte[]"

    if fld.proto_type in WRAPPER_MAP:
        cs, nullable = WRAPPER_MAP[fld.proto_type]
        if for_params:
            return f"{cs}?" if nullable else cs
        return cs

    ns, name = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
    if name in api_enums or name in rt_enums:
        enum_ns = api_enums.get(name) or rt_enums.get(name) or ns
        cs = f"global::{enum_ns}.{name}"
        return f"{cs}?" if for_params else cs
    if name in api_msgs or name in rt_msgs:
        return params_type_name(name) if for_params else data_type_name(name)

    cs = scalar_proto_to_cs(fld.proto_type, for_params)
    if for_params and cs in {"int", "long", "uint", "ulong", "bool", "float", "double"}:
        return f"{cs}?"
    if for_params and cs == "string":
        return "string?"
    return cs


def scalar_proto_to_cs(proto_type: str, for_params: bool) -> str:
    if proto_type in WRAPPER_MAP:
        cs, _ = WRAPPER_MAP[proto_type]
        return cs
    mapping = {
        "int32": "int",
        "sint32": "int",
        "sfixed32": "int",
        "fixed32": "uint",
        "int64": "long",
        "sint64": "long",
        "sfixed64": "long",
        "fixed64": "ulong",
        "uint32": "uint",
        "uint64": "ulong",
        "bool": "bool",
        "string": "string",
        "bytes": "byte[]",
        "float": "float",
        "double": "double",
    }
    return mapping.get(proto_type, proto_type)


def response_type_name(proto_name: str) -> str:
    if proto_name == "LoginIDResponse":
        return "LoginIdResponse"
    if proto_name.endswith("Response"):
        return proto_name
    return f"{proto_name}Response"


def data_type_name(proto_name: str) -> str:
    """Backward-compatible alias used internally by generator."""
    return response_type_name(proto_name)


def params_type_name(proto_name: str) -> str:
    if proto_name == "AccountMezon":
        return "AccountMezonBodyParams"
    if proto_name == "ChannelMessageSend":
        return "SendChannelMessageParams"
    if proto_name.endswith("Request"):
        return f"{proto_name[:-7]}Params"
    return f"{proto_name}Params"


def event_data_type_name(proto_name: str) -> str:
    if proto_name == "Session":
        return "Session"
    return f"{proto_name}EventData"


def strip_type(type_raw: str) -> str:
    t = type_raw.strip()
    t = t.replace("global::", "")
    t = re.sub(r"\s+", " ", t)
    return t


def short_type_name(type_raw: str) -> str:
    t = strip_type(type_raw)
    if "." in t:
        return t.split(".")[-1]
    return t


def parse_method_args(args_raw: str) -> List[Tuple[str, str]]:
    args_raw = args_raw.strip()
    if not args_raw:
        return []
    parts: List[str] = []
    depth = 0
    current: List[str] = []
    for ch in args_raw:
        if ch == "<":
            depth += 1
        elif ch == ">":
            depth -= 1
        elif ch == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
            continue
        current.append(ch)
    if current:
        parts.append("".join(current).strip())

    result: List[Tuple[str, str]] = []
    for part in parts:
        part = part.strip()
        if not part:
            continue
        if "=" in part:
            part = part.split("=", 1)[0].strip()
        tokens = part.rsplit(" ", 1)
        if len(tokens) != 2:
            continue
        result.append((tokens[0].strip(), tokens[1].strip()))
    return result


def parse_api_methods(text: str) -> List[ApiMethod]:
    iface_match = re.search(r"interface\s+IMezonApiClient[^{]*\{", text)
    if not iface_match:
        return []
    body = text[iface_match.end() :]
    depth = 1
    i = 0
    while i < len(body) and depth > 0:
        if body[i] == "{":
            depth += 1
        elif body[i] == "}":
            depth -= 1
        i += 1
    body = body[: i - 1]

    methods: List[ApiMethod] = []
    pattern = re.compile(
        r"^\s*Task(?:<(?P<ret>[^>]+)>)?\s+(?P<name>\w+Async)\s*\((?P<args>.*?)\)\s*;",
        re.MULTILINE | re.DOTALL,
    )
    for m in pattern.finditer(body):
        name = m.group("name")
        if name in SKIP_METHODS:
            continue
        if name.startswith("Configure"):
            continue
        ret_raw = (m.group("ret") or "").strip()
        args_raw = m.group("args").strip()
        method = ApiMethod(name, ret_raw, args_raw, parse_method_args(args_raw))
        skip = False
        for rule_name, rule_fn in SKIP_METHOD_RULES:
            if method.name == rule_name and rule_fn(method):
                skip = True
                break
        if skip:
            continue
        methods.append(method)
    return dedupe_methods(methods)


def parse_socket_overrides(text: str) -> Set[str]:
    names = set(
        re.findall(
            r"public\s+override\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|Task)\s+(\w+Async)\s*\(",
            text,
        )
    )
    return names


def parse_socket_events(text: str, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> List[SocketEvent]:
    events: List[SocketEvent] = []
    pattern = re.compile(
        r"public\s+event\s+Func<([^,>]+),\s*Task>\s+(\w+)\s*\{",
        re.MULTILINE,
    )
    for m in pattern.finditer(text):
        payload = strip_type(m.group(1))
        event_name = m.group(2)
        if payload in ("Task", "string"):
            continue
        short = short_type_name(payload)
        if short == "Task":
            continue

        payload_ns = ""
        if payload.startswith("Internal.Api.") or f"{API_NS}." in payload or payload.startswith("Mezon.Net.Internal.Api."):
            payload_ns = API_NS
            short = short_type_name(payload)
        elif payload.startswith("Internal.Realtime.") or f"{RT_NS}." in payload or payload.startswith("Mezon.Net.Internal.Realtime."):
            payload_ns = RT_NS
            short = short_type_name(payload)
        elif short in rt_msgs:
            payload_ns = RT_NS
        elif short in api_msgs:
            payload_ns = API_NS
        else:
            continue

        field_name = "_" + event_name[0].lower() + event_name[1:]
        if field_name.endswith("Event"):
            field_name = field_name[:-5] + "Event"
        field_name = re.sub(r"([a-z])([A-Z])", r"\1_\2", field_name).lower()
        field_name = field_name.replace("__", "_")
        if not field_name.startswith("_"):
            field_name = "_" + field_name

        events.append(SocketEvent(event_name, field_name, short, payload_ns))
    return events


def is_proto_type(type_name: str, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> bool:
    short = short_type_name(type_name)
    return short in api_msgs or short in rt_msgs


def is_primitive_param(type_name: str) -> bool:
    t = strip_type(type_name)
    if t.startswith("IEnumerable<") or t.startswith("IReadOnly"):
        inner = re.match(r"IEnumerable<([^>]+)>", t)
        if inner:
            return is_primitive_param(inner.group(1))
    if t.endswith("?"):
        t = t[:-1]
    if t in PRIMITIVE_CS:
        return True
    if t == "RequestOptions":
        return True
    if t.startswith("System.IO.Stream"):
        return True
    short = short_type_name(t)
    if short in {"RequestOptions"}:
        return True
    return False


def has_basic_auth(args: List[Tuple[str, str]]) -> bool:
    names = {n for _, n in args}
    return "basicAuthUsername" in names and "basicAuthPassword" in names


def find_proto_body(args: List[Tuple[str, str]], api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> Optional[Tuple[str, str, str]]:
    for type_raw, name in args:
        if name in ("options", "basicAuthUsername", "basicAuthPassword"):
            continue
        short = short_type_name(type_raw)
        if short in CUSTOM_BODY_TO_PROTO:
            mapped = CUSTOM_BODY_TO_PROTO[short]
            if mapped:
                return mapped, name, type_raw
            continue
        if is_proto_type(type_raw, api_msgs, rt_msgs):
            return short, name, type_raw
    return None


def facade_return_type(ret_raw: str) -> Tuple[str, bool]:
    """Returns (facade type, needs_wrap). needs_wrap=False for Task/Stream/pass-through."""
    if not ret_raw:
        return "Task", False
    ret = strip_type(ret_raw)
    if ret in ("MezonSession", "Session") or ret.endswith(".Session") or ret == f"{API_NS}.Session":
        return "Session", True
    if ret == "LoginIDResponse":
        return "LoginIdResponse", True
    if ret in ("System.IO.Stream", "Stream"):
        return "Stream", False
    if ret in ("Empty", "Google.Protobuf.WellKnownTypes.Empty"):
        return "Task", False
    short = short_type_name(ret)
    if short == "Empty":
        return "Task", False
    if is_proto_type(ret, api_messages, rt_messages):
        return data_type_name(short), True
    return ret, False


def facade_type_ref(type_name: str) -> str:
    """Fully-qualified public facade type (avoids collision with legacy Client/Internal.Api types)."""
    if type_name in ("Task", "Stream", "Session"):
        return type_name
    if "." in type_name:
        return type_name
    return f"{NS_MODELS}.{type_name}"


def proto_cs_type_name(
    msg: ProtoMessage,
    api_msgs: Dict[str, ProtoMessage],
    rt_msgs: Dict[str, ProtoMessage],
) -> str:
    if msg.parent:
        parent = api_msgs.get(msg.parent) or rt_msgs.get(msg.parent)
        if parent:
            parent_cs = proto_cs_type_name(parent, api_msgs, rt_msgs)
            return f"{parent_cs}.Types.{msg.name}"
        return f"global::{msg.namespace}.{msg.parent}.Types.{msg.name}"
    return f"global::{msg.namespace}.{msg.name}"


def proto_cs_type_name_by_name(
    name: str,
    api_msgs: Dict[str, ProtoMessage],
    rt_msgs: Dict[str, ProtoMessage],
) -> str:
    msg = api_msgs.get(name) or rt_msgs.get(name)
    if msg:
        return proto_cs_type_name(msg, api_msgs, rt_msgs)
    return f"global::{API_NS}.{name}"


def gen_proto_list_view() -> str:
    return f"""// <auto-generated />
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Collections;

namespace {NS_MODELS}
{{
    /// <summary>
    ///     Lightweight read-only view over protobuf repeated fields. Nested projections are materialized once.
    /// </summary>
    public readonly struct ProtoListView<TData> : IReadOnlyList<TData>
    {{
        private readonly IReadOnlyList<TData> _items;

        internal ProtoListView(IReadOnlyList<TData> items) => _items = items ?? Array.Empty<TData>();

        public int Count => _items.Count;
        public TData this[int index] => _items[index];

        public IEnumerator<TData> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal static ProtoListView<TData> FromRepeated(RepeatedField<TData> field)
            => field is null || field.Count == 0
                ? new ProtoListView<TData>(Array.Empty<TData>())
                : new ProtoListView<TData>(field);

        internal static ProtoListView<TData> FromRepeated<TProto>(
            RepeatedField<TProto> field,
            Func<TProto, TData> factory)
        {{
            if (field is null || field.Count == 0)
                return new ProtoListView<TData>(Array.Empty<TData>());
            var arr = new TData[field.Count];
            for (var i = 0; i < field.Count; i++)
                arr[i] = factory(field[i]);
            return new ProtoListView<TData>(arr);
        }}
    }}
}}
"""


def gen_data_struct(msg: ProtoMessage, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> str:
    type_name = response_type_name(msg.name)
    proto_cs = proto_cs_type_name(msg, api_msgs, rt_msgs)
    lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System.Collections.Generic;",
        "",
        f"namespace {NS_MODELS}",
        "{",
        f"    public readonly struct {type_name}",
        "    {",
        f"        private readonly {proto_cs} _proto;",
        f"        internal {type_name}({proto_cs} proto) => _proto = proto;",
        f"        internal {proto_cs} Proto => _proto;",
        "",
    ]
    seen_props: Set[str] = set()
    for fld in msg.fields:
        public_prop = public_property_name(msg, fld)
        proto_prop = proto_csharp_property_name(msg, fld)
        if public_prop in seen_props:
            continue
        seen_props.add(public_prop)
        if fld.repeated:
            inner_fld = ProtoField(fld.name, fld.proto_type, fld.number)
            _, inner_name = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
            if inner_name in api_msgs or inner_name in rt_msgs:
                inner_data = response_type_name(inner_name)
                lines.append(
                    f"        public ProtoListView<{inner_data}> {public_prop} => "
                    f"ProtoListView<{inner_data}>.FromRepeated(_proto.{proto_prop}, x => new {inner_data}(x));"
                )
            elif fld.proto_type == "bytes":
                lines.append(
                    f"        public ProtoListView<byte[]> {public_prop} => "
                    f"ProtoListView<byte[]>.FromRepeated(_proto.{proto_prop}, x => x.ToByteArray());"
                )
            else:
                inner = scalar_proto_to_cs(fld.proto_type, False)
                lines.append(
                    f"        public ProtoListView<{inner}> {public_prop} => "
                    f"ProtoListView<{inner}>.FromRepeated(_proto.{proto_prop});"
                )
        elif fld.proto_type == "map":
            cs_type = proto_field_cs_type(fld, api_msgs, rt_msgs, for_params=False)
            lines.append(f"        public {cs_type} {public_prop} => _proto.{proto_prop};")
        elif fld.proto_type == "bytes":
            lines.append(f"        public byte[] {public_prop} => _proto.{proto_prop}.ToByteArray();")
        elif fld.proto_type in WRAPPER_MAP:
            cs, _ = WRAPPER_MAP[fld.proto_type]
            if cs == "string":
                lines.append(f"        public {cs} {public_prop} => _proto.{proto_prop} ?? \"\";")
            else:
                lines.append(f"        public {cs} {public_prop} => _proto.{proto_prop} ?? default;")
        else:
            _, inner_name = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
            if inner_name in api_enums or inner_name in rt_enums:
                cs_type = proto_field_cs_type(fld, api_msgs, rt_msgs, for_params=False)
                lines.append(f"        public {cs_type} {public_prop} => _proto.{proto_prop};")
            elif inner_name in api_msgs or inner_name in rt_msgs:
                inner_data = response_type_name(inner_name)
                lines.append(
                    f"        public {inner_data} {public_prop} => new {inner_data}(_proto.{proto_prop});"
                )
            else:
                cs_type = scalar_proto_to_cs(fld.proto_type, False)
                lines.append(f"        public {cs_type} {public_prop} => _proto.{proto_prop};")
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def gen_event_data_struct(msg: ProtoMessage, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> str:
    if msg.name == "Session":
        return ""
    type_name = event_data_type_name(msg.name)
    data_name = response_type_name(msg.name)
    return f"""// <auto-generated />
#nullable enable

namespace {NS_MODELS}
{{
    public readonly struct {type_name}
    {{
        private readonly {data_name} _data;
        internal {type_name}({data_name} data) => _data = data;
        internal {data_name} Data => _data;

        public static implicit operator {data_name}({type_name} e) => e._data;
        public static implicit operator {type_name}({data_name} d) => new {type_name}(d);
    }}
}}
"""


def gen_params_struct(msg: ProtoMessage, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> str:
    type_name = params_type_name(msg.name)
    lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System.Collections.Generic;",
        "",
        f"namespace {NS_MODELS}",
        "{",
        f"    public readonly struct {type_name}",
        "    {",
    ]
    for fld in msg.fields:
        prop = snake_to_pascal(fld.name)
        cs_type = proto_field_cs_type(fld, api_msgs, rt_msgs, for_params=True)
        lines.append(f"        public readonly {cs_type} {prop};")
    if msg.fields:
        ctor_params = []
        ctor_assign = []
        for fld in msg.fields:
            prop = snake_to_pascal(fld.name)
            cs_type = proto_field_cs_type(fld, api_msgs, rt_msgs, for_params=True)
            if cs_type.endswith("?") or cs_type == "byte[]":
                ctor_params.append(f"{cs_type} {camel_case(prop)} = null")
            else:
                ctor_params.append(f"{cs_type} {camel_case(prop)} = default")
            ctor_assign.append(f"            {prop} = {camel_case(prop)};")
        lines.append(f"        public {type_name}({', '.join(ctor_params)})")
        lines.append("        {")
        lines.extend(ctor_assign)
        lines.append("        }")
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def camel_case(name: str) -> str:
    if not name:
        return name
    return name[0].lower() + name[1:]


def proto_csharp_property_name(msg: ProtoMessage, fld: ProtoField) -> str:
    if fld.name == "e2ee":
        return "E2Ee"
    prop = snake_to_pascal(fld.name)
    if prop == msg.name:
        return f"{prop}_"
    return prop


def public_property_name(msg: ProtoMessage, fld: ProtoField) -> str:
    prop = proto_csharp_property_name(msg, fld)
    return prop[:-1] if prop.endswith("_") else prop


def gen_mapper(msg: ProtoMessage, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> str:
    params_name = params_type_name(msg.name)
    proto_cs = proto_cs_type_name(msg, api_msgs, rt_msgs)
    mapper_name = f"{params_name}Mapper"
    lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System.Linq;",
        f"using {API_NS};",
        f"using {RT_NS};",
        f"using {NS_MODELS};",
        "",
        f"namespace {NS_MAPPER}",
        "{",
        f"    internal static class {mapper_name}",
        "    {",
        f"        internal static {proto_cs} ToProto(in {params_name} p)",
        "        {",
        f"            var proto = new {proto_cs}();",
    ]
    for fld in msg.fields:
        prop = snake_to_pascal(fld.name)
        proto_prop = proto_csharp_property_name(msg, fld)
        if fld.repeated:
            _, inner_name = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
            if inner_name in api_msgs or inner_name in rt_msgs:
                inner_params = params_type_name(inner_name)
                lines.append(f"            if (p.{prop} is not null)")
                lines.append(f"            {{")
                lines.append(f"                foreach (var item in p.{prop})")
                lines.append(f"                    proto.{proto_prop}.Add({inner_params}Mapper.ToProto(item));")
                lines.append(f"            }}")
            elif fld.proto_type in api_enums or fld.proto_type in rt_enums:
                lines.append(f"            if (p.{prop} is not null)")
                lines.append(f"            {{")
                lines.append(f"                foreach (var item in p.{prop})")
                lines.append(f"                    if (item.HasValue) proto.{proto_prop}.Add(item.Value);")
                lines.append(f"            }}")
            else:
                inner_cs = scalar_proto_to_cs(fld.proto_type, True).rstrip("?")
                if inner_cs in {"int", "long", "uint", "ulong", "bool", "float", "double"}:
                    lines.append(f"            if (p.{prop} is not null)")
                    lines.append(f"            {{")
                    lines.append(f"                foreach (var item in p.{prop})")
                    lines.append(f"                    if (item.HasValue) proto.{proto_prop}.Add(item.Value);")
                    lines.append(f"            }}")
                else:
                    lines.append(f"            if (p.{prop} is not null)")
                    lines.append(f"                proto.{proto_prop}.AddRange(p.{prop});")
        elif fld.proto_type == "map":
            lines.append(f"            if (p.{prop} is not null)")
            lines.append(f"            {{")
            lines.append(f"                foreach (var kv in p.{prop})")
            lines.append(f"                    proto.{proto_prop}[kv.Key] = kv.Value;")
            lines.append(f"            }}")
        elif fld.proto_type == "bytes":
            lines.append(f"            if (p.{prop} is not null)")
            lines.append(f"                proto.{proto_prop} = Google.Protobuf.ByteString.CopyFrom(p.{prop});")
        elif fld.proto_type in WRAPPER_MAP:
            cs, _ = WRAPPER_MAP[fld.proto_type]
            if cs == "string":
                lines.append(f"            if (p.{prop} is not null)")
                lines.append(f"                proto.{proto_prop} = p.{prop};")
            else:
                lines.append(f"            if (p.{prop}.HasValue)")
                lines.append(f"                proto.{proto_prop} = p.{prop}.Value;")
        elif fld.proto_type in api_enums or fld.proto_type in rt_enums:
            cs_type = proto_field_cs_type(fld, api_msgs, rt_msgs, for_params=True)
            if cs_type.endswith("?"):
                lines.append(f"            if (p.{prop}.HasValue)")
                lines.append(f"                proto.{proto_prop} = p.{prop}.Value;")
            else:
                lines.append(f"            proto.{proto_prop} = p.{prop};")
        else:
            ns, inner_name = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
            if inner_name in api_msgs or inner_name in rt_msgs:
                inner_params = params_type_name(inner_name)
                inner_mapper = f"{inner_params}Mapper"
                cs_type = proto_field_cs_type(fld, api_msgs, rt_msgs, for_params=True)
                if cs_type.endswith("?"):
                    lines.append(f"            if (p.{prop}.HasValue)")
                    lines.append(f"                proto.{proto_prop} = {inner_mapper}.ToProto(p.{prop}.Value);")
                else:
                    lines.append(f"            proto.{proto_prop} = {inner_mapper}.ToProto(p.{prop});")
            elif fld.proto_type in {"int32", "int64", "uint32", "uint64", "bool", "float", "double", "sint32", "sint64"}:
                lines.append(f"            if (p.{prop}.HasValue)")
                lines.append(f"                proto.{proto_prop} = p.{prop}.Value;")
            elif fld.proto_type == "string":
                lines.append(f"            if (p.{prop} is not null)")
                lines.append(f"                proto.{proto_prop} = p.{prop};")
            else:
                lines.append(f"            if (p.{prop} is not null)")
                lines.append(f"                proto.{proto_prop} = p.{prop};")
    lines.extend(
        [
            "            return proto;",
            "        }",
            "    }",
            "}",
            "",
        ]
    )
    return "\n".join(lines)


def facade_param_list(method: ApiMethod, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> Tuple[str, str]:
    """Returns (signature params, call args for ApiClient)."""
    basic_auth = has_basic_auth(method.args)
    proto_body = find_proto_body(method.args, api_msgs, rt_msgs)
    sig_parts: List[str] = []
    call_parts: List[str] = []

    if basic_auth:
        pass  # stripped from public facade

    if proto_body:
        proto_name, arg_name, type_raw = proto_body
        short = short_type_name(type_raw)
        if short == "SendChannelMessageParams":
            sig_parts.append(f"{NS_MODELS}.SendChannelMessageParams {arg_name}")
            call_parts.append(arg_name)
        elif short in CUSTOM_BODY_TO_PROTO and CUSTOM_BODY_TO_PROTO[short] is None:
            sig_parts.append(f"{strip_type(type_raw)} {arg_name}")
            call_parts.append(arg_name)
        else:
            params_type = params_type_name(proto_name)
            sig_parts.append(f"{NS_MODELS}.{params_type} {arg_name}")
            call_parts.append(f"{params_type}Mapper.ToProto({arg_name})")

    for type_raw, name in method.args:
        if name in ("options", "basicAuthUsername", "basicAuthPassword"):
            if name == "options":
                sig_parts.append("RequestOptions? options = null")
                call_parts.append("options")
            continue
        if proto_body and name == proto_body[1]:
            continue
        sig_parts.append(f"{strip_type(type_raw)} {name}")
        call_parts.append(name)

    if basic_auth:
        call_parts = ["Options.ServerKey", "\"\""] + call_parts

    return ", ".join(sig_parts), ", ".join(call_parts)


def gen_method_impl(method: ApiMethod, *, is_socket: bool, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> str:
    facade_ret, wrap = facade_return_type(method.ret_raw)
    sig_params, call_args = facade_param_list(method, api_msgs, rt_msgs)
    is_void = facade_ret == "Task" and not method.ret_raw
    is_empty_ret = facade_ret == "Task" and method.ret_raw and short_type_name(method.ret_raw) == "Empty"

    if is_void or is_empty_ret:
        return f"""        public async Task {method.name}({sig_params})
        {{
            await ApiClient.{method.name}({call_args}).ConfigureAwait(false);
        }}"""

    if facade_ret == "Stream":
        return f"""        public async Task<Stream> {method.name}({sig_params})
        {{
            return await ApiClient.{method.name}({call_args}).ConfigureAwait(false);
        }}"""

    if facade_ret == "Session":
        return f"""        public async Task<Session> {method.name}({sig_params})
        {{
            var result = await ApiClient.{method.name}({call_args}).ConfigureAwait(false);
            return new Session(result);
        }}"""

    if wrap:
        ref = facade_type_ref(facade_ret)
        return f"""        public async Task<{ref}> {method.name}({sig_params})
        {{
            var result = await ApiClient.{method.name}({call_args}).ConfigureAwait(false);
            return new {ref}(result);
        }}"""

    ref = facade_type_ref(facade_ret)
    return f"""        public async Task<{ref}> {method.name}({sig_params})
        {{
            return await ApiClient.{method.name}({call_args}).ConfigureAwait(false);
        }}"""


def gen_iface_method(method: ApiMethod, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> str:
    facade_ret, _ = facade_return_type(method.ret_raw)
    sig_params, _ = facade_param_list(method, api_msgs, rt_msgs)
    if facade_ret == "Task" and not method.ret_raw:
        return f"        Task {method.name}({sig_params});"
    return f"        Task<{facade_type_ref(facade_ret)}> {method.name}({sig_params});"


def collect_used_types(methods: List[ApiMethod], api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage]) -> Tuple[Set[str], Set[str]]:
    return_types: Set[str] = set()
    request_types: Set[str] = set()

    for m in methods:
        if m.ret_raw:
            short = short_type_name(m.ret_raw)
            if short not in ("Empty", "Stream", "MezonSession", "Session") and short != "System.IO.Stream":
                if is_proto_type(m.ret_raw, api_msgs, rt_msgs) or short in api_msgs or short in rt_msgs:
                    return_types.add(short)
            if short == "LoginIDResponse":
                return_types.add(short)

        body = find_proto_body(m.args, api_msgs, rt_msgs)
        if body:
            request_types.add(body[0])

        for type_raw, name in m.args:
            if name in ("options", "basicAuthUsername", "basicAuthPassword"):
                continue
            short = short_type_name(type_raw)
            if is_proto_type(type_raw, api_msgs, rt_msgs):
                if not body or name != body[1]:
                    request_types.add(short)

    # Include ChannelMessageSend for SendChannelMessageParams mapper generation.
    if any(m.name == "SendChannelMessageAsync" for m in methods):
        request_types.add("ChannelMessageSend")

    return return_types, request_types


def collect_nested_messages(msg_name: str, api_msgs: Dict[str, ProtoMessage], rt_msgs: Dict[str, ProtoMessage], seen: Set[str]) -> Set[str]:
    if msg_name in seen:
        return seen
    seen.add(msg_name)
    msg = api_msgs.get(msg_name) or rt_msgs.get(msg_name)
    if not msg:
        return seen
    for fld in msg.fields:
        if fld.proto_type == "map":
            _, val = resolve_proto_type(fld.map_value or "string", api_msgs, rt_msgs)
            if val in api_msgs or val in rt_msgs:
                collect_nested_messages(val, api_msgs, rt_msgs, seen)
        elif not fld.repeated:
            _, inner = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
            if inner in api_msgs or inner in rt_msgs:
                collect_nested_messages(inner, api_msgs, rt_msgs, seen)
        else:
            _, inner = resolve_proto_type(fld.proto_type, api_msgs, rt_msgs)
            if inner in api_msgs or inner in rt_msgs:
                collect_nested_messages(inner, api_msgs, rt_msgs, seen)
    return seen


def event_field_name(event_name: str) -> str:
    return "_" + event_name[0].lower() + event_name[1:]


# Fallback when BaseMezonSocketClient.Events.cs no longer lists protobuf payload types.
SOCKET_EVENT_MANIFEST: List[Tuple[str, str, str]] = [
    ("PongReceivedEvent", "Pong", RT_NS),
    ("ChannelReceivedEvent", "Channel", RT_NS),
    ("ClanJoinedEvent", "ClanJoin", RT_NS),
    ("ChannelJoinedEvent", "ChannelJoin", RT_NS),
    ("ChannelLeftEvent", "ChannelLeave", RT_NS),
    ("ChannelMessageReceivedEvent", "ChannelMessage", API_NS),
    ("ChannelMessageAckReceivedEvent", "ChannelMessageAck", API_NS),
    ("ChannelMessageSentEvent", "ChannelMessageSend", API_NS),
    ("ChannelMessageUpdatedEvent", "ChannelMessageUpdate", API_NS),
    ("ChannelMessageRemovedEvent", "ChannelMessageRemove", API_NS),
    ("ChannelPresenceChangedEvent", "ChannelPresenceEvent", RT_NS),
    ("ErrorReceivedEvent", "Error", RT_NS),
    ("NotificationsReceivedEvent", "Notifications", API_NS),
    ("RpcReceivedEvent", "Rpc", API_NS),
    ("StatusReceivedEvent", "Status", RT_NS),
    ("StatusFollowedEvent", "StatusFollow", RT_NS),
    ("StatusPresenceChangedEvent", "StatusPresenceEvent", RT_NS),
    ("StatusUnfollowedEvent", "StatusUnfollow", RT_NS),
    ("StatusUpdatedEvent", "StatusUpdate", RT_NS),
    ("StreamDataReceivedEvent", "StreamData", RT_NS),
    ("StreamPresenceChangedEvent", "StreamPresenceEvent", RT_NS),
    ("MessageTypingReceivedEvent", "MessageTypingEvent", RT_NS),
    ("LastSeenMessageUpdatedEvent", "LastSeenMessageEvent", RT_NS),
    ("MessageReactionReceivedEvent", "MessageReaction", API_NS),
    ("VoiceJoinedEvent", "VoiceJoinedEvent", RT_NS),
    ("VoiceLeavedEvent", "VoiceLeavedEvent", RT_NS),
    ("VoiceStartedEvent", "VoiceStartedEvent", RT_NS),
    ("VoiceEndedEvent", "VoiceEndedEvent", RT_NS),
    ("ChannelCreatedEvent", "ChannelCreatedEvent", RT_NS),
    ("ChannelDeletedEvent", "ChannelDeletedEvent", RT_NS),
    ("ChannelUpdatedEvent", "ChannelUpdatedEvent", RT_NS),
    ("LastPinMessageUpdatedEvent", "LastPinMessageEvent", RT_NS),
    ("CustomStatusChangedEvent", "CustomStatusEvent", RT_NS),
    ("UserChannelAddedEvent", "UserChannelAdded", RT_NS),
    ("UserChannelRemovedEvent", "UserChannelRemoved", RT_NS),
    ("ClanUserAddedEvent", "AddClanUserEvent", RT_NS),
    ("RoleAssignedEvent", "RoleAssignedEvent", RT_NS),
    ("RoleChangedEvent", "RoleEvent", RT_NS),
    ("LocalCacheUpdatedEvent", "ApiRequestEvent", RT_NS),
    ("ApiRequestReceivedEvent", "ApiRequestEvent", RT_NS),
    ("ChannelUsersBannedListedEvent", "ListChannelUsersBannedEvent", RT_NS),
    ("SessionRefreshedEvent", "Session", API_NS),
    ("ChannelArchivedEvent", "ChannelArchiveEvent", RT_NS),
    ("TopicInMessageReceivedEvent", "TopicInMessageEvent", RT_NS),
    ("ScreenShareReceivedEvent", "ScreenShareEvent", RT_NS),
    ("MessageButtonClickedEvent", "MessageButtonClicked", RT_NS),
    ("DropdownBoxSelectedEvent", "DropdownBoxSelected", RT_NS),
]


def manifest_socket_events() -> List[SocketEvent]:
    events: List[SocketEvent] = []
    for name, payload, ns in SOCKET_EVENT_MANIFEST:
        field = event_field_name(name)
        events.append(SocketEvent(name=name, payload_type=payload, payload_ns=ns, field_name=field))
    return events


def gen_events_file(events: List[SocketEvent]) -> str:
    lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System;",
        "using System.Threading.Tasks;",
        "using Mezon.Net.Core;",
        f"using {NS_MODELS};",
        f"using {NS_CLIENT};",
        "",
        f"namespace {NS_CLIENT}",
        "{",
        "    public abstract partial class BaseMezonSocketClient",
        "    {",
    ]
    for ev in events:
        if ev.payload_type == "Session":
            payload = "Session"
        else:
            payload = event_data_type_name(ev.payload_type)
        field_name = event_field_name(ev.name)
        lines.extend(
            [
                f"        public event Func<{payload}, Task> {ev.name}",
                "        {",
                f"            add {{ {field_name}.Add(value); }}",
                f"            remove {{ {field_name}.Remove(value); }}",
                "        }",
                f"        internal readonly AsyncEvent<Func<{payload}, Task>> {field_name} = new AsyncEvent<Func<{payload}, Task>>();",
                "",
            ]
        )
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def parse_realtime_transport_methods(transport_path: Path) -> Set[str]:
    if not transport_path.is_file():
        return set()
    text = read_text(transport_path)
    names: Set[str] = set()
    for match in re.finditer(
        r"(?:async\s+)?(\w+)\s*\([^)]*\)\s*(?::\s*[^{]+)?\s*\{[^}]{0,4000}?const\s+urlPath\s*=\s*\"\"",
        text,
        re.DOTALL,
    ):
        names.add(match.group(1))
    return names


def assert_realtime_transport_parity() -> None:
    manifest = {m.js_name for m in REALTIME_METHODS}
    if len(REALTIME_METHODS) != 21:
        raise RuntimeError(f"REALTIME_METHODS must contain 21 entries, got {len(REALTIME_METHODS)}")
    if len(manifest) != 21:
        raise RuntimeError("REALTIME_METHODS contains duplicate js_name entries")
    if not MEZON_JS_TRANSPORT.is_file():
        print(f"Warning: mezon-js transport not found at {MEZON_JS_TRANSPORT}; skipping transport parity assert")
        return
    transport = parse_realtime_transport_methods(MEZON_JS_TRANSPORT)
    missing = transport - manifest
    extra = manifest - transport
    if missing or extra:
        raise RuntimeError(
            f"REALTIME_METHODS mismatch with transport.ts urlPath=\"\": missing={sorted(missing)} extra={sorted(extra)}"
        )


def resolve_realtime_body_expr(method: RealtimeMethod) -> str:
    expr = method.body_expr
    if "{Mapper}" in expr and method.params_type:
        mapper = f"{method.params_type}Mapper"
        expr = expr.replace("{Mapper}", mapper)
    return expr


def gen_realtime_method_impl(method: RealtimeMethod) -> str:
    body_expr = resolve_realtime_body_expr(method)
    if method.no_params:
        sig = "RequestOptions? options = null"
    else:
        sig = f"{NS_MODELS}.{method.params_type} {method.param_name}, RequestOptions? options = null"

    if method.build_full_envelope:
        envelope_line = f"var envelope = {body_expr};"
    elif method.no_params:
        envelope_line = f"var envelope = new global::Mezon.Net.Internal.Realtime.Envelope {{ {method.envelope_property} = {body_expr} }};"
    else:
        envelope_line = (
            f"var envelope = new global::Mezon.Net.Internal.Realtime.Envelope "
            f"{{ {method.envelope_property} = {body_expr} }};"
        )

    if method.response_data_type:
        ref = facade_type_ref(method.response_data_type)
        return f"""        /// <summary>Realtime envelope send ({method.envelope_property}). mezon-js: {method.js_name}.</summary>
        public async Task<{ref}> {method.dotnet_name}({sig})
        {{
            {envelope_line}
            var response = await ApiClient.SendRtAwaitAckAsync(envelope, options).ConfigureAwait(false);
            return new {ref}(response.{method.response_property});
        }}"""

    return f"""        /// <summary>Realtime envelope send ({method.envelope_property}). mezon-js: {method.js_name}.</summary>
        public async Task {method.dotnet_name}({sig})
        {{
            {envelope_line}
            await ApiClient.SendRtAsync(envelope, options).ConfigureAwait(false);
        }}"""


def gen_realtime_facade() -> str:
    lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System.Threading.Tasks;",
        "using Mezon.Net.Abstractions;",
        "using Mezon.Net.Core;",
        f"using {NS_MODELS};",
        f"using {NS_MAPPER};",
        "using Mezon.Net.Client.Messaging;",
        "",
        f"namespace {NS_CLIENT}",
        "{",
        "    public abstract partial class BaseMezonSocketClient",
        "    {",
    ]
    for method in REALTIME_METHODS:
        lines.append(gen_realtime_method_impl(method))
        lines.append("")
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def response_file_stem(path: Path) -> str:
    name = path.name
    if name.endswith(".g.cs"):
        return name[: -len(".g.cs")]
    return path.stem


STALE_GENERATED_FILES = (
    "IMezonClientApi.g.cs",
    "IMezonClientRealtime.g.cs",
    "BaseSocketClient.Api.g.cs",
    "BaseSocketClient.Realtime.g.cs",
    "BaseSocketClient.Events.Data.g.cs",
)


def cleanup_stale_generated_files() -> int:
    removed = 0
    for name in STALE_GENERATED_FILES:
        path = OUT_GENERATED / name
        if path.is_file():
            path.unlink()
            removed += 1
    return removed


def cleanup_stale_response_files(valid_stems: Set[str]) -> int:
    removed = 0
    for path in OUT_RESPONSES.glob("*.g.cs"):
        if response_file_stem(path) not in valid_stems:
            path.unlink()
            removed += 1
    return removed


def main() -> int:
    global api_messages, rt_messages, api_enums, rt_enums
    assert_realtime_transport_parity()
    api_messages, api_enums = parse_proto_messages(API_PROTO, API_NS)
    rt_messages, rt_enums = parse_proto_messages(REALTIME_PROTO, RT_NS)

    iface_text = read_text(API_INTERFACE)
    socket_text = read_text(SOCKET_CLIENT)
    events_text = read_text(EVENTS_FILE)

    methods = parse_api_methods(iface_text)
    socket_methods = parse_socket_overrides(socket_text)
    events = parse_socket_events(events_text, api_messages, rt_messages)
    if not events:
        events = manifest_socket_events()

    return_types, request_types = collect_used_types(methods, api_messages, rt_messages)

    for ev in events:
        if ev.payload_type != "Session":
            return_types.add(ev.payload_type)

    all_data_types: Set[str] = set()
    for name in return_types:
        all_data_types |= collect_nested_messages(name, api_messages, rt_messages, set())

    # expand request nested types
    all_request_types: Set[str] = set()
    for name in request_types:
        all_request_types |= collect_nested_messages(name, api_messages, rt_messages, set())

    for rt in REALTIME_METHODS:
        if (
            rt.request_proto
            and rt.request_proto not in SKIP_REQUEST_PROTO
            and rt.params_type
            and rt.params_type not in HAND_WRITTEN_PARAMS
        ):
            all_request_types |= collect_nested_messages(rt.request_proto, api_messages, rt_messages, set())

    generated = 0

    write_file(OUT_RESPONSES / "ProtoListView.g.cs", gen_proto_list_view())
    generated += 1

    response_file_stems: Set[str] = {"ProtoListView"}
    for name in sorted(all_data_types):
        msg = api_messages.get(name) or rt_messages.get(name)
        if not msg:
            continue
        file_stem = response_type_name(name)
        if name in HAND_WRITTEN_RESPONSES:
            continue
        response_file_stems.add(file_stem)
        write_file(OUT_RESPONSES / f"{file_stem}.g.cs", gen_data_struct(msg, api_messages, rt_messages))
        generated += 1

    removed = cleanup_stale_response_files(response_file_stems)
    if removed:
        print(f"  Removed {removed} stale response files")

    for name in sorted(all_request_types):
        if name in SKIP_REQUEST_PROTO:
            continue
        msg = api_messages.get(name) or rt_messages.get(name)
        if not msg:
            continue
        params_file = params_type_name(name)
        if params_file in HAND_WRITTEN_PARAMS:
            continue
        write_file(OUT_REQUESTS / f"{params_file}.g.cs", gen_params_struct(msg, api_messages, rt_messages))
        generated += 1
        write_file(OUT_REQUESTS / f"{params_file}Mapper.g.cs", gen_mapper(msg, api_messages, rt_messages))
        generated += 1

    for ev in events:
        if ev.payload_type == "Session":
            continue
        msg = api_messages.get(ev.payload_type) or rt_messages.get(ev.payload_type)
        if not msg:
            continue
        content = gen_event_data_struct(msg, api_messages, rt_messages)
        if content:
            write_file(OUT_EVENTS / f"{event_data_type_name(ev.payload_type)}.g.cs", content)
            generated += 1

    removed_generated = cleanup_stale_generated_files()
    if removed_generated:
        print(f"  Removed {removed_generated} stale generated files")

    rest_methods = [m for m in methods if m.name not in socket_methods]
    socket_api_methods = [m for m in methods if m.name in socket_methods]

    rest_lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System.Collections.Generic;",
        "using System.IO;",
        "using System.Threading.Tasks;",
        "using Mezon.Net.Abstractions;",
        "using Mezon.Net.Core;",
        f"using {NS_MODELS};",
        f"using {NS_MAPPER};",
        f"using {NS_CLIENT};",
        "",
        f"namespace {NS_CLIENT}",
        "{",
        "    public abstract partial class BaseMezonClient",
        "    {",
    ]
    for m in rest_methods:
        rest_lines.append(gen_method_impl(m, is_socket=False, api_msgs=api_messages, rt_msgs=rt_messages))
        rest_lines.append("")
    rest_lines.extend(["    }", "}", ""])
    write_file(OUT_GENERATED / "BaseMezonClient.Api.g.cs", "\n".join(rest_lines))
    generated += 1

    socket_lines = [
        "// <auto-generated />",
        "#nullable enable",
        "using System.Collections.Generic;",
        "using System.IO;",
        "using System.Threading.Tasks;",
        "using Mezon.Net.Abstractions;",
        "using Mezon.Net.Core;",
        f"using {NS_MODELS};",
        f"using {NS_MAPPER};",
        f"using {NS_CLIENT};",
        "",
        f"namespace {NS_CLIENT}",
        "{",
        "    public abstract partial class BaseMezonSocketClient",
        "    {",
    ]
    for m in socket_api_methods:
        socket_lines.append(gen_method_impl(m, is_socket=True, api_msgs=api_messages, rt_msgs=rt_messages))
        socket_lines.append("")
    socket_lines.extend(["    }", "}", ""])
    write_file(OUT_GENERATED / "BaseMezonSocketClient.Api.g.cs", "\n".join(socket_lines))
    generated += 1

    write_file(OUT_GENERATED / "BaseMezonSocketClient.Events.Data.g.cs", gen_events_file(events))
    generated += 1

    write_file(OUT_GENERATED / "BaseMezonSocketClient.Realtime.g.cs", gen_realtime_facade())
    generated += 1

    print(f"Generated {generated} files")
    print(f"  API methods: {len(methods)} ({len(rest_methods)} REST, {len(socket_api_methods)} socket)")
    print(f"  Realtime methods: {len(REALTIME_METHODS)}")
    print(f"  Response types: {len(all_data_types)}")
    print(f"  Request types: {len(all_request_types)}")
    print(f"  Socket events with payload: {len(events)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
