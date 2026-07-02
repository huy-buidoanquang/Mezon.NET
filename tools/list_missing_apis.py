import re
map_text = open('src/Mezon.Net.Core/Protocol/ApiNameIndexMap.cs').read()
impl_text = open('src/Mezon.Net.Client/Clients/MezonSocketApiClient.cs').read()
impl_text += open('src/Mezon.Net.Client/MezonSocketApiClient.Engine.cs').read()
map_names = re.findall(r'\["([^"]+)"\]', map_text)
impl_names = set(re.findall(r'SendApiAsync\("([^"]+)"', impl_text))
missing = [n for n in map_names if n not in impl_names]
print('Map', len(map_names), 'Impl', len(impl_names), 'Missing', len(missing))
for n in missing:
    print(n)
