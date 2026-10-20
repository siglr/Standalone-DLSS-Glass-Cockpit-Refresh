"""Stage existing signed release files and listing copy; never uploads or signs."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT=Path(__file__).resolve().parents[1]
PRODUCT='Standalone-DLSS-Glass-Cockpit-Refresh'
OUT=ROOT/'build/standalone/server-upload'
PREP=ROOT/'build/release-preparation'

def main():
    version=(ROOT/'standalone/VERSION').read_text().strip()
    feed=json.loads((OUT/(PRODUCT+'.version.json')).read_text())
    assert feed['version']==version
    assert hashlib.sha256((OUT/(PRODUCT+'.zip')).read_bytes()).hexdigest()==feed['sha256']
    assert (OUT/(PRODUCT+'.zip')).stat().st_size==feed['size']
    notes=(ROOT/'standalone/RELEASE-NOTES.md').read_text(encoding='utf-8')
    assert len(notes)<=2000, 'HangarAddons changelog must fit its 2,000-character limit'
    (PREP/'HANGAR-CHANGELOG.txt').write_text(notes,encoding='utf-8')
    deployment=PREP/'deployment';deployment.mkdir(exist_ok=True)
    files=[PRODUCT+suffix for suffix in ('.zip','.zip.sha256','-source.zip','-source.zip.sha256','.hashes.json','.html')]
    files+=['index.html','.htaccess']
    files+=['assets/'+name for name in ('glass-cockpit-refresh.png','glass-cockpit-refresh-settings.png',
                                      'glass-cockpit-refresh-light.png','glass-cockpit-refresh-settings-light.png')]
    bundles={'standalone-publish-current.zip':files,
             'standalone-publish-update-feed.zip':[PRODUCT+'.version.json']}
    for name,contents in bundles.items():
        with zipfile.ZipFile(deployment/name,'w',zipfile.ZIP_DEFLATED) as archive:
            for item in contents:archive.write(OUT/item,item)
    report={'version':version,'published':False,'platforms':['GitHub','WSG','Flightsim.to','HangarAddons'],
            'normalDownload':str(OUT/(PRODUCT+'.zip')),'changelogCharacters':len(notes),
            'deploymentOrder':list(bundles),'compatibility':'Existing immutable compatibility files must remain on WSG.',
            'screenshots':'Existing four user-supplied screenshots retained; keep videos on both listing sites.',
            'files':{name:hashlib.sha256((deployment/name).read_bytes()).hexdigest() for name in bundles}}
    (PREP/'PREPARED-RELEASE.json').write_text(json.dumps(report,indent=2)+'\n')
    print('Prepared platform copy and two WSG upload bundles for',version,'— not published.')

if __name__=='__main__':main()
