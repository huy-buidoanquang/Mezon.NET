"""Add override keyword to MezonSocketApiClient API methods."""
import re
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FILES = [
    os.path.join(ROOT, "src/Mezon.Net.Client/Clients/MezonSocketApiClient.cs"),
    os.path.join(ROOT, "src/Mezon.Net.Client/MezonSocketApiClient.Engine.cs"),
]

for path in FILES:
    text = open(path, encoding="utf-8").read()
    text = re.sub(
        r"public (async )?(override )?(Task(?:<[^>]+>)?) (\w+Async)\(",
        lambda m: f"public override {m.group(1) or ''}{m.group(3)} {m.group(4)}("
        if m.group(4) not in ("SendApiAsync", "SendApiEnvelopeAsync", "SendEnvelopeAsync", "SendSocketPayloadAsync")
        else m.group(0),
        text,
    )
    open(path, "w", encoding="utf-8").write(text)
    print("patched", os.path.basename(path))
