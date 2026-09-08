import io, re

SLOTS = ["drone", "trooper", "sniper", "mech", "shield-bot",
         "interceptor", "hacker", "titan", "turret"]
THEMES = ["CYBER", "SYNTHWAVE", "BIOPUNK", "MEDIEVAL",
          "INDUSTRIAL", "SAKURA", "SOLAR", "DAWN"]

loc = io.open('ThemeLocale.cs', encoding='utf-8').read()
art = io.open('NeonArt.cs', encoding='utf-8').read()

# --- parse ThemeLocale.ArtId remaps per theme ---
body = loc[loc.index('public static string ArtId('):]
body = body[:body.index('\n        }')]
remap = {t: {} for t in range(8)}
cur = None
for line in body.split('\n'):
    m = re.match(r'\s*(\d) => canonicalId switch', line)
    if m:
        cur = int(m.group(1)); continue
    m = re.match(r'\s*"([\w\-]+)"\s*=>\s*"([\w\-]+)"', line)
    if m and cur is not None:
        remap[cur][m.group(1)] = m.group(2)

# --- parse the BuildUnit3D switch labels ---
b3 = art[art.index('static Texture2D BuildUnit3D('):]
b3 = b3[:b3.index('P3DOutline(px, R);')]
dispatched = set(re.findall(r'case "([\w\-]+)":', b3))

def reg(name):
    seg = art[art.index('bool ' + name + '('):]
    # Strip // comments BEFORE looking for the terminator. A ';' inside a comment is
    # legal C# but used to truncate this scan, silently dropping every id after it and
    # reporting ids as unregistered when they were registered fine.
    seg = re.sub(r'//.*', '', seg)
    seg = seg[:seg.index(';', seg.index('=>'))]
    return set(re.findall(r'"([\w\-]+)"', seg))

has_poses = reg('HasPoses')
has_atk   = reg('HasAttackPoses')
side_rig  = reg('UsesSideRig')
authored  = reg('IsAuthoredArt')
# HasAttackPoses is `UsesSideRig(id) || id is ...`
has_atk |= side_rig

print("theme        slot          art id        dispatch  poses  attack  authored")
print("-" * 76)
problems = []
built_count = {}
for t in range(8):
    n = 0
    for slot in SLOTS:
        aid = remap[t].get(slot, slot)
        d = aid in dispatched
        pz = aid in has_poses
        at = aid in has_atk
        au = aid in authored
        if au:
            n += 1
        flag = ""
        if not d:
            flag = "  <-- NO DISPATCH"; problems.append((THEMES[t], slot, aid, "no dispatch"))
        elif not pz:
            flag = "  <-- no walk cycle"; problems.append((THEMES[t], slot, aid, "no poses"))
        print(f"{THEMES[t]:<12} {slot:<13} {aid:<13} "
              f"{'y' if d else 'N':^8} {'y' if pz else '-':^6} {'y' if at else '-':^7} "
              f"{'y' if au else '-':^8}{flag}")
    built_count[THEMES[t]] = n
    print()

print("AUTHORED-ART COVERAGE")
for t in THEMES:
    print(f"  {t:<12} {built_count[t]}/9")
print()
print("PROBLEMS:", len(problems))
for pr in problems:
    print("  ", pr)
