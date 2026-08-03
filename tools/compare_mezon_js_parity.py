#!/usr/bin/env python3
"""Compare mezon-js transport/client surface with Mezon.Net generated facades."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MEZON_JS_TRANSPORT = ROOT.parent / "mezon-js" / "packages" / "mezon-js" / "transport.ts"
MEZON_JS_CLIENT = ROOT.parent / "mezon-js" / "packages" / "mezon-js" / "client.ts"
DOTNET_API_FACADE = ROOT / "src" / "Mezon.Net.Client" / "Generated" / "BaseMezonSocketClient.Api.g.cs"
DOTNET_RT_FACADE = ROOT / "src" / "Mezon.Net.Client" / "Generated" / "BaseMezonSocketClient.Realtime.g.cs"

# mezon-js realtime transport method -> .NET Rt method
REALTIME_JS_TO_DOTNET = {
    "followUsers": "FollowUsersRtAsync",
    "joinClanChat": "JoinClanChatRtAsync",
    "follower": "FollowerRtAsync",
    "joinChat": "JoinChannelChatRtAsync",
    "leaveChat": "LeaveChannelChatRtAsync",
    "removeChatMessage": "RemoveChatMessageRtAsync",
    "unfollowUsers": "UnfollowUsersRtAsync",
    "updateChatMessage": "UpdateChatMessageRtAsync",
    "updateStatus": "UpdateStatusRtAsync",
    "writeQuickMenuEvent": "SendQuickMenuEventRtAsync",
    "writeEphemeralMessage": "SendEphemeralMessageRtAsync",
    "writeChatMessage": "SendChatMessageRtAsync",
    "writeMessageReaction": "SendMessageReactionRtAsync",
    "writeMessageTyping": "SendMessageTypingRtAsync",
    "writeLastSeenMessage": "SendLastSeenMessageRtAsync",
    "writeLastPinMessage": "SendLastPinMessageRtAsync",
    "writeCustomStatus": "SendCustomStatusRtAsync",
    "writeVoiceReaction": "SendVoiceReactionRtAsync",
    "forwardWebrtcSignaling": "ForwardWebrtcSignalingRtAsync",
    "makeCallPush": "MakeCallPushRtAsync",
    "writeChannelAppEvent": "SendChannelAppEventRtAsync",
}


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def parse_transport_realtime_methods(text: str) -> set[str]:
    names: set[str] = set()
    for match in re.finditer(
        r"(?:async\s+)?(\w+)\s*\([^)]*\)\s*(?::\s*[^{]+)?\s*\{[^}]{0,4000}?const\s+urlPath\s*=\s*\"\"",
        text,
        re.DOTALL,
    ):
        names.add(match.group(1))
    return names


def parse_transport_api_methods(text: str) -> set[str]:
    names: set[str] = set()
    for match in re.finditer(r'urlPath\s*=\s*"/mezon\.api\.Mezon/(\w+)"', text):
        names.add(match.group(1))
    return names


def parse_realtime_facade_methods(text: str) -> set[str]:
    return set(re.findall(r"public async Task(?:<[^>]+>)? (\w+RtAsync)\s*\(", text))


def parse_api_facade_methods(text: str) -> set[str]:
    return set(re.findall(r"public async Task(?:<[^>]+>)? (\w+Async)\s*\(", text))


def camel_to_pascal(name: str) -> str:
    return name[:1].upper() + name[1:]


def main() -> int:
    errors: list[str] = []

    rt_text = read_text(DOTNET_RT_FACADE)
    dotnet_rt = parse_realtime_facade_methods(rt_text)
    expected_rt = set(REALTIME_JS_TO_DOTNET.values())

    missing_rt = expected_rt - dotnet_rt
    extra_rt = dotnet_rt - expected_rt
    if len(dotnet_rt) != 21:
        errors.append(f"BaseMezonSocketClient.Realtime expected 21 methods, found {len(dotnet_rt)}")
    if missing_rt:
        errors.append(f"Missing realtime methods: {sorted(missing_rt)}")
    if extra_rt:
        errors.append(f"Unexpected realtime methods: {sorted(extra_rt)}")

    if MEZON_JS_TRANSPORT.is_file():
        transport = read_text(MEZON_JS_TRANSPORT)
        js_rt = parse_transport_realtime_methods(transport)
        js_api = parse_transport_api_methods(transport)

        missing_js_rt = js_rt - set(REALTIME_JS_TO_DOTNET.keys())
        extra_js_rt = set(REALTIME_JS_TO_DOTNET.keys()) - js_rt
        if missing_js_rt:
            errors.append(f"mezon-js transport realtime not mapped: {sorted(missing_js_rt)}")
        if extra_js_rt:
            errors.append(f"Mapping includes methods absent from transport.ts: {sorted(extra_js_rt)}")

        api_text = read_text(DOTNET_API_FACADE)
        dotnet_api = parse_api_facade_methods(api_text)

        # Socket API names in transport are PascalCase RPC names; .NET adds Async suffix.
        mapped_api = {camel_to_pascal(n) + "Async" for n in js_api}
        unmapped_transport_api = sorted(mapped_api - dotnet_api)
        if unmapped_transport_api:
            print(f"Note: {len(unmapped_transport_api)} transport socket APIs have no facade match (REST-only or renamed).")
            print("  Sample:", ", ".join(unmapped_transport_api[:8]))

        print(f"transport.ts socket API methods: {len(js_api)}")
        print(f"transport.ts realtime methods: {len(js_rt)}")
        print(f"BaseMezonSocketClient.Api methods: {len(dotnet_api)}")
    else:
        print(f"Warning: transport.ts not found at {MEZON_JS_TRANSPORT}")

    print(f"BaseMezonSocketClient.Realtime methods: {len(dotnet_rt)}")

    if errors:
        for err in errors:
            print(f"ERROR: {err}", file=sys.stderr)
        return 1

    print("Parity check passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
