"""Xbox/Steam discovery using inert files only; never launches MSFS or edits registry."""
from pathlib import Path
import json, os, subprocess, tempfile, sys
from fixture_cleanup import cleanup_fixture

ROOT = Path(__file__).resolve().parents[1]
WORK = Path(tempfile.mkdtemp(prefix='window-tests-', dir=ROOT/'build')).resolve()
SHELL = Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'
env = os.environ.copy()
env['PSModulePath'] = str(SHELL.parent/'Modules')
checks = 0

def detect(root, expected=None, error=None):
    global checks
    command=[sys.argv[sys.argv.index('--native')+1], 'discover', str(root)]
    r = subprocess.run(command,
        capture_output=True, text=True, env=env, timeout=20)
    if expected:
        assert r.returncode == 0 and r.stdout.strip().casefold() == str(expected).casefold(), r.stdout+r.stderr
    else:
        assert r.returncode != 0 and (error is None or error in r.stderr), r.stdout+r.stderr
    checks += 1

def game(path, xbox=False):
    path.mkdir(parents=True, exist_ok=True)
    (path/'FlightSimulator2024.exe').write_text('INERT MARKER - NEVER EXECUTE')
    if xbox:
        (path/'MicrosoftGame.Config').write_text('<Game><Identity Name="Microsoft.Limitless" /></Game>')
    return path

def manifest(library, install='Microsoft Flight Simulator 2024', appid='2537590'):
    steamapps=library/'steamapps'; steamapps.mkdir(parents=True, exist_ok=True)
    target=steamapps/'appmanifest_2537590.acf'
    target.write_text('"AppState" { "appid" "'+appid+'" "installdir" '+json.dumps(install)+' }')
    return target

try:
    empty=WORK/'empty';empty.mkdir();detect(empty)
    xbox=WORK/'xbox';target=game(xbox/'XboxGames/Flight Sim/Content', True)
    detect(xbox,target);detect(target,target)
    steam=WORK/'Steam';manifest(steam)
    target=game(steam/'steamapps/common/Microsoft Flight Simulator 2024')
    detect(steam,target);detect(target,target)
    # Real current Valve layout, nested apps, comments, escaped Windows paths.
    main=WORK/'Main Steam';(main/'steamapps').mkdir(parents=True)
    library=WORK/"Other drive library O'Brien";manifest(library,"Custom MSFS folder")
    target=game(library/'steamapps/common/Custom MSFS folder')
    vdf=main/'steamapps/libraryfolders.vdf'
    vdf.write_text('"libraryfolders" { // custom library\n "0" { "path" '+json.dumps(str(main))+
        ' "apps" { "2537590" "12345" } } "1" { "path" '+json.dumps(str(library))+
        ' "apps" { "2537590" "98765" } } "2" { "path" "Z:\\\\Missing" } }')
    detect(main,target)
    # Legacy path values, duplicate library spelling, and fallback config location.
    vdf.write_text('"LibraryFolders" { "0" '+json.dumps(str(library))+' "1" '+json.dumps(str(library).upper())+' }')
    detect(main,target)
    (main/'config').mkdir();vdf.replace(main/'config/libraryfolders.vdf')
    vdf.write_text('"libraryfolders" { "broken"')
    detect(main,target)
    # Missing exe and wrong app ID never select another game or streaming content.
    (target/'FlightSimulator2024.exe').unlink();detect(main)
    game(target);manifest(library,"Custom MSFS folder",'1250410');detect(main)
    manifest(library,"..\\..\\outside");game(library/'outside');detect(main)
    manifest(library,str(WORK/'outside'));game(WORK/'outside');detect(main)
    manifest(library,"Custom MSFS folder")
    second=game(main/'XboxGames/Second MSFS/Content',True)
    detect(main,error='Multiple MSFS 2024 installations found')
    (second/'FlightSimulator2024.exe').unlink()
    protected=WORK/'WindowsApps/Protected Steam';manifest(protected)
    protected_game=game(protected/'steamapps/common/Microsoft Flight Simulator 2024')
    detect(protected);detect(protected_game)
    wrong=game(WORK/'Wrong Xbox',True)
    (wrong/'MicrosoftGame.Config').write_text('<Game><Identity Name="Other.Game" /></Game>')
    detect(wrong)
    # Malformed manifests fail closed, without hiding a separate Xbox installation.
    (library/'steamapps/appmanifest_2537590.acf').write_text('"AppState" { "appid" "2537590"')
    detect(main)
    game(second,True);detect(main,second)
    print(f'PASS: {checks} isolated Steam/Xbox discovery checks')
finally:
    cleanup_fixture(WORK)
