"""Release copy, screenshot links and shared-index scope. Reads staged files only."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit
import hashlib,json,re,struct

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'build/standalone/server-upload'
PRODUCT='Standalone-DLSS-Glass-Cockpit-Refresh'
class Page(HTMLParser):
    def __init__(self):super().__init__();self.images=[];self.links=[];self.ids=[]
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='img':self.images.append(a)
        if tag=='a':self.links.append(a.get('href',''))
        if 'id' in a:self.ids.append(a['id'])

page=(OUT/(PRODUCT+'.html')).read_text(encoding='utf-8')
parsed=Page();parsed.feed(page)
assert len(parsed.ids)==len(set(parsed.ids))
assert all(h[1:] in parsed.ids for h in parsed.links if h.startswith('#'))
assert '_SCREENSHOT_HASH' not in page
hero=page.split('<section class="hero"',1)[1].split('</section>',1)[0]
assert '<div class="instrument" role="img"' in hero and '<figure' not in hero
assert 'Do not install this standalone package' not in page
assert 'ReShade/add-on conflicts' not in page
assert all(text in page for text in ('ReShade add-on','Settings','Latest official release','same official version and mode installed manually','version.dll','Source'))
for image in parsed.images:
    if 'glass-cockpit-refresh' not in image.get('src',''):continue
    u=urlsplit(image['src']);p=OUT/u.path
    digest=hashlib.sha256(p.read_bytes()).hexdigest()[:16]
    assert u.query=='v='+digest and image.get('alt')
    assert p.read_bytes()[:8]==b'\x89PNG\r\n\x1a\n'
    dimensions=struct.unpack('>II',p.read_bytes()[16:24])
    assert all(n>300 for n in dimensions)
    assert (int(image['width']),int(image['height']))==dimensions
assert len([i for i in parsed.images if 'glass-cockpit-refresh' in i.get('src','')])==4
assert 'test build' in page and 'release downloads have automatic app updates enabled' in page
stage=json.loads((ROOT/'build/release-preparation/STAGING.json').read_text())
baseline=Path(stage['sharedIndexSource']).read_text()
index=(OUT/'index.html').read_text()
def cards(text):return re.findall(r'<article class="tool">.*?</article>',text,re.S)
assert [c for c in cards(index) if PRODUCT not in c]==[c for c in cards(baseline) if PRODUCT not in c]
assert 'WSG-OpenXR-FG-Switcher.html' in index
assert 'ReShade components included' not in index
assert 'ReShade add-on' in index
assert {p.name for p in (OUT/'assets').iterdir()}=={'glass-cockpit-refresh.png','glass-cockpit-refresh-settings.png','glass-cockpit-refresh-light.png','glass-cockpit-refresh-settings-light.png'}
for name in ('RELEASE-NOTES.md','RELEASE-DESCRIPTION.md','RELEASE-UPLOADS.md'):
    assert (ROOT/'build/release-preparation'/name).read_bytes()==(ROOT/'standalone'/name).read_bytes()
assert not any(p.suffix=='.md' for p in OUT.iterdir())
print('PASS release page links, current two-mode copy, screenshot cache keys and unchanged other-tool cards')
