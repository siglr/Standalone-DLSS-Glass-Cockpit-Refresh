"""Execute the actual overlay's gain/seam decisions on D3D12, without MSFS.

Extract rather than duplicate the production calculations. The full renderer
suite separately covers their integration. --baseline uses the previous source
to demonstrate that these cases reject the regression.
"""
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
baseline = '--baseline' in sys.argv
shader = (subprocess.check_output(['git', 'show', 'fb628223d51c7c9d5609470806f3e132c2b85b45:vendor/glass/src/overlay.hlsl'], cwd=ROOT, text=True)
          if baseline else (ROOT/'vendor/glass/src/overlay.hlsl').read_text())
gain = shader[shader.index('    float3 a, b;'):shader.index('    // Fit quality:')]
start = shader.index('            float lab =')
end = shader.index('\n            {\n                // Two sides:', start)
seam = shader[start:end]
# Return the production branch decision, keeping its exact predicate.
seam += '\n                return true;\n    return false;\n'
outer = ''
if 'float outer_step(' in shader:
    outer = shader[shader.index('float outer_step('):shader.index('void continue_from(')]
wrapper = '''
RWStructuredBuffer<float> results : register(u0);
static uint scenario = 0;
uint map_slot(float4 m) { return uint(m.w); }
float4 load_map(int2 p) {
    // Smooth 10:1 texture stretch, a true discontinuity, and a bezel band.
    float2 uv = float2(p.x * 0.003, p.y * 0.03);
    if (scenario == 1) uv.y = p.y * 0.003 + (p.x >= 1 ? 0.4 : 0);
    if (scenario == 2) uv = p.x < 1 ? float2(0, 0) : float2(0.4 + p.x * 0.003, p.y * 0.003);
    return float4(uv, 0, 1);
}
''' + outer + '''
bool detect_seam() {
    int2 cell = int2(0,0);
    float4 a=load_map(cell), b=load_map(cell+int2(1,0)), c=load_map(cell+int2(0,1)), d=load_map(cell+int2(1,1));
''' + seam + '''
}
[numthreads(1,1,1)] void main(uint3 id : SV_DispatchThreadID) {
    // Red has genuine contrast. Tiny green/blue contributions must not be
    // amplified into a different colour by glare in the rendered scene.
    double3 variance=double3(0.1,0,0), covariance=double3(0.2,0,0);
    double3 mx=double3(0.3,0.0002,0.0002), my=double3(0.6,2.6,1.8);
''' + gain + '''
    results[0]=a.x; results[1]=a.y; results[2]=a.z;
    for(uint n=0;n<3;++n) { scenario=n; results[3+n]=detect_seam()?1:0; }
}
'''
with tempfile.TemporaryDirectory(prefix='hotfix-shader-', dir=ROOT/'build') as directory:
    work = Path(directory)
    (work/'probe.hlsl').write_text(wrapper)
    cache = (ROOT/'build/standalone/CMakeCache.txt').read_text()
    dxc = next(line.split('=', 1)[1] for line in cache.splitlines() if line.startswith('DXC:FILEPATH='))
    subprocess.run([dxc, '-T', 'cs_6_6', '-HV', '2021', '-E', 'main', '-WX', '-O3', '-Fo', str(work/'probe.cso'), str(work/'probe.hlsl')], check=True)
    result = subprocess.run([str(ROOT/'build/standalone/Release/shader_hotfix_probe.exe'), str(work/'probe.cso')])
    assert result.returncode == (1 if baseline else 0), 'Unexpected shader regression result'
if not baseline:
    source = (ROOT/'vendor/glass/src/glass_cockpit_dlss_fix.cpp').read_text()
    late = source[source.index('if (!relevant && new_module'):source.index('if (!relevant || !seen.insert')]
    assert 'file_version(' not in late and 'file_company(' not in late, 'Late-loaded protected modules must not be opened'
print('PASS previous shader rejected' if baseline else 'PASS GPU gain/seam regressions and protected-module guard')
