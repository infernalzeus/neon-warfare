"""
NEON WARFARE -- Invoke AI asset generator
Architecture: torch_directml + diffusers (CPU->DML->CPU UNet bridge, CPU VAE decode)
Ported from Chronicle Forge image_gen.py to fix Windows GPU/CPU encode-decode timeouts.

Run:  python gen_nw_assets.py
      python gen_nw_assets.py --regen        # regenerate even if file exists
      python gen_nw_assets.py --dry-run      # print prompts only, no generation

Outputs PNG files to Assets/_Project/Resources/Art/
"""

import gc
import os
import random
import sys
import time

import numpy as np

# Force UTF-8 console on Windows
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

MODEL_PATH = r"Z:\Programs\Invoke\models\0b629f4f-bf32-4a49-88df-52b8c8c87ec6"
OUT_DIR    = r"N:\Code\git repositories\_unsorted projects\neon-warfare\Assets\_Project\Resources\Art"
STEPS      = 28
CFG        = 7.5

# ── Negative prompt (shared) ──────────────────────────────────────────────────

NEG = (
    "text, watermark, logo, signature, border, ui frame, "
    "blurry, low quality, ugly, deformed, extra limbs, mutation, "
    "duplicate, multiple subjects, noisy, grainy, overexposed"
)

NEG_SPRITE = NEG + ", background, scenery, environment, landscape"

# ── Asset definitions ─────────────────────────────────────────────────────────
# Each entry:
#   name   -- output filename (no extension)
#   w, h   -- generation resolution (must be multiples of 8; SDXL prefers 512-1216)
#   steps  -- denoising steps
#   cfg    -- classifier-free guidance scale
#   clip   -- short subject for CLIP ViT-L  (<= 20 words)
#   full   -- full cinematic prompt for OpenCLIP ViT-bigG
#   neg    -- negative prompt (defaults to NEG_SPRITE)

ASSETS = [
    # ── Troop sprites 512x512 ─────────────────────────────────────────────────
    dict(
        name="troop_drone", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="small quadrotor combat drone, neon cyan thrusters, black body",
        full=(
            "small autonomous quadrotor combat drone, cyberpunk military design, "
            "matte black chassis, glowing cyan thruster rings, isolated on pure black, "
            "full body game character concept art, dramatic rim lighting, 8k sharp"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_trooper", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="cyberpunk infantry soldier, powered exo-armor, neon blue visor",
        full=(
            "cyberpunk military trooper, full body side view, powered exo-armor, "
            "electric blue visor, dark urban camo, isolated on pure black, "
            "game character concept art, cinematic lighting, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_sniper", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="cyberpunk sniper, stealth suit, long energy rifle, laser sight",
        full=(
            "cyberpunk long-range sniper, sleek stealth exo-suit, energy rifle with scope, "
            "red laser sight glow, full body side view, isolated on pure black, "
            "game character concept art, 8k sharp"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_mech", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="heavy bipedal battle mech, dual cannons, orange heat vents",
        full=(
            "heavy cyberpunk battle mech, bipedal war machine, dual heavy cannons, "
            "thick armor plating, glowing orange heat vents, full body side profile, "
            "isolated on pure black, game asset concept art, cinematic lighting, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_shield_bot", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="defensive robot, large energy shield emitter, holographic blue barrier",
        full=(
            "defensive shield robot, broad energy shield emitter on one arm, "
            "heavy armored chassis, holographic blue barrier glow, "
            "full body side view, isolated on pure black, "
            "cyberpunk game character concept art, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_interceptor", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="sleek cyberpunk fighter jet, swept wings, purple plasma engines",
        full=(
            "sleek cyberpunk fighter interceptor jet, twin purple plasma engines, "
            "swept delta wings, angular stealth silhouette, side view, "
            "isolated on pure black, sci-fi game asset concept art, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_hacker", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="cyberpunk hacker, holographic data streams, glowing neural jack",
        full=(
            "cyberpunk hacker specialist, lightweight combat suit, "
            "holographic data streams floating around hands, neural jack glowing at temple, "
            "full body side view, isolated on pure black, "
            "game character concept art, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_titan", w=512, h=512, steps=35, cfg=7.5,
        clip="massive siege titan mech, towering armored war machine, red glowing cores",
        full=(
            "massive siege titan mech, enormous bipedal war machine, "
            "multiple weapon hardpoints, glowing red heat cores, "
            "full body side profile, isolated on pure black, "
            "cinematic game concept art, dramatic lighting, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="troop_turret", w=512, h=512, steps=STEPS, cfg=CFG,
        clip="automated defense turret, triple rotating barrels, radar dish, neon targeting laser",
        full=(
            "cyberpunk automated defense turret, triple rotating barrels, "
            "radar dish on top, neon blue targeting laser, armored base, "
            "full body front-side view, isolated on pure black, "
            "game asset art, 8k"
        ),
        neg=NEG_SPRITE,
    ),

    # ── Battlefield background 1536x640 (SDXL ultra-wide native) ────────────
    dict(
        name="bg_battlefield", w=1536, h=640, steps=40, cfg=7.0,
        clip="cyberpunk urban war street, neon-soaked cracked asphalt, blue plasma fire, destroyed city blocks",
        full=(
            "war-torn cyberpunk city street at night, cracked and scorched asphalt reflecting "
            "brilliant electric-blue neon signs from shattered storefronts, "
            "overturned military vehicles with glowing hydraulics, smoke columns rising into a "
            "dark smoggy sky pierced by searchlights, spent shell casings and holographic debris "
            "scattered across the lane, tactical ground markings still faintly glowing blue, "
            "ultra-detailed dystopian environment, no characters, cinematic wide-angle low-horizon "
            "composition, dramatic directional lighting, photorealistic 8k game background art"
        ),
        neg=NEG + ", characters, people, soldiers, weapons in hands, bright daylight, clean streets, cartoon, anime, nature, trees",
    ),

    # ── Level-select screen background 1536x832 ───────────────────────────────
    dict(
        name="bg_level_select", w=1536, h=832, steps=40, cfg=7.0,
        clip="cyberpunk command center interior, panoramic holographic tactical war room, dark blue cyan ambience",
        full=(
            "vast cyberpunk military command center interior, towering curved walls lined with "
            "enormous holographic tactical display screens showing strategic maps and troop deployment grids, "
            "dark blue and electric cyan neon lighting casting long cool shadows, "
            "polished dark floor reflecting the screen glow, catwalks and observation decks at multiple heights, "
            "thin haze of atmospheric smoke in the upper rafters, "
            "sci-fi architecture with geometric brutalist columns wrapped in circuit panel motifs, "
            "epic sense of scale and depth, no people, ultra-wide panoramic environment art, 8k cinematic digital painting"
        ),
        neg=NEG + ", people, soldiers, nature, daylight windows, warm orange tones, medieval, cartoon, anime, clutter, low ceiling",
    ),

    # ── Theme battlefield backgrounds 1536x640 (SDXL ultra-wide native) ─────
    dict(
        name="bg_battlefield_purple", w=1536, h=640, steps=40, cfg=7.0,
        clip="synthwave retrowave grid battlefield, neon purple horizon, chrome platform, violet twin suns dusk",
        full=(
            "infinite retrowave synthwave battle platform, perfect chrome perspective grid receding "
            "to a violet horizon under twin suns, electric purple and hot magenta neon horizon glow, "
            "mirror-polished floor reflecting the glowing grid lines and a deep violet sky with "
            "slow-moving neon aurora bands, distant dark city silhouette of razor-sharp pyramidal "
            "megastructures, low fog catching neon pink light near the ground, no characters, "
            "ultra-wide cinematic ground-level panoramic shot, high contrast dramatic lighting, 8k digital painting"
        ),
        neg=NEG + ", blue tones, green, brown, organic, trees, nature, daytime, bright white sky, people, soldiers, modern military",
    ),
    dict(
        name="bg_battlefield_green", w=1536, h=640, steps=40, cfg=7.0,
        clip="biopunk wasteland battlefield, acid-green mycelia networks, corroded grating, toxic bioluminescent fog",
        full=(
            "biopunk industrial wasteland battlefield, corroded steel grating walkways fused with "
            "dense luminescent mycelia networks pulsing bright acid-green, pools of glowing toxic "
            "sludge casting green light from below, massive fungal stalks erupting through cracked "
            "factory floors, spore clouds drifting at ground level, rusted vats and broken pipes "
            "wrapped in living tissue, the atmosphere thick with green-tinted bioluminescent fog, "
            "no characters, wide desolate arena ground-level view, eerie and oppressive, 8k cinematic digital painting"
        ),
        neg=NEG + ", purple, blue neon, warm orange, clean sterile lab, bright daylight, cartoon, anime, fantasy medieval, flowers, people",
    ),
    dict(
        name="bg_battlefield_crimson", w=1536, h=640, steps=40, cfg=7.0,
        clip="dark gothic medieval battlefield, ancient blood-red stone ramparts, ember-lit runes, scarlet stormy sky",
        full=(
            "ancient dark fantasy battlefield, massive blood-red stone ramparts and crumbling gothic "
            "battlements stretching across the frame, crimson ember-carved runic glyphs burning in "
            "the flagstone floor tiles, burning siege equipment smoldering in the mid-ground, "
            "scarlet lightning cracking through a churning storm sky, hellish orange firelight from "
            "torches reflected in polished dark stone, fallen banners with occult sigils, "
            "heavy fog pooling at ground level catching the red glow, no characters, "
            "wide dramatic low-angle cinematic painting, 8k"
        ),
        neg=NEG + ", modern technology, sci-fi ships, blue neon, green nature, bright clean daylight, anime, cartoonish, purple synthwave, people",
    ),
    dict(
        name="bg_battlefield_ghost", w=1536, h=640, steps=40, cfg=7.0,
        clip="desolate abandoned factory arena, pale monochrome, phosphorescent white grid, heavy industrial fog",
        full=(
            "desolate abandoned industrial mega-factory battlefield, towering derelict machinery "
            "and collapsed conveyor gantries, ghostly monochrome palette of cold whites, pale greys, "
            "and deep charcoals, dim phosphorescent tactical grid lines glowing faint ice-white in "
            "the floor, thick heavy fog blanketing the ground and filling cavernous bay spaces, "
            "pale arc-light shafts cutting through shattered skylights, peeling paint and oxidized "
            "steel grating, absolute silence implied in the composition, no characters, "
            "wide cinematic overhead mood shot, 8k digital painting"
        ),
        neg=NEG + ", colorful, warm orange, vibrant neon, purple, green, blue, red, nature, trees, daylight, anime, fantasy, clean, people",
    ),
    dict(
        name="bg_battlefield_sakura", w=1536, h=640, steps=40, cfg=7.0,
        clip="japanese temple courtyard at twilight, sakura petals drifting, stone lanterns, amber dusk mist",
        full=(
            "ancient japanese temple-garden battlefield at deep twilight, stone-paved courtyard "
            "tactical grid stretching wide, dense cherry blossom sakura petals in motion blur "
            "drifting low across the ground, warm amber paper lanterns hanging and glowing overhead "
            "in long rows, torii gate silhouettes in the misty indigo background distance, "
            "raked gravel patterns and mossy stone textures in the foreground, soft pink and warm "
            "amber gradient sky meeting deep purple-blue zenith, wisps of low ground mist catching "
            "lantern glow, no characters, wide painterly cinematic low composition, 8k"
        ),
        neg=NEG + ", modern neon signs, sci-fi, cold blue steel, grey industrial, harsh military, bright sunlight, anime, cartoon, Christmas lights, people",
    ),
    dict(
        name="bg_battlefield_solar", w=1536, h=640, steps=40, cfg=7.0,
        clip="volcanic stellar forge battlefield, molten gold lava rivers, obsidian platform, blinding solar furnace above",
        full=(
            "volcanic solar forge battlefield platform, rivers of churning molten gold and blazing "
            "orange lava carving channels through obsidian black rock and dark tactical grid tiles, "
            "extreme heat shimmer distorting the mid-ground and horizon, blinding white-gold solar "
            "core hanging overhead casting harsh top-down amber-white light with hard shadows, "
            "glowing slag chunks and upward-drifting embers, raised forge platforms with magma vents "
            "erupting, the entire scene bathed in incandescent hellfire glow, no characters, "
            "ultra-wide dramatic cinematic overhead composition, 8k"
        ),
        neg=NEG + ", cold blue tones, snow, ice, green nature, purple, muted grey, dark shadows, night, moonlight, anime, cartoon, people",
    ),
    dict(
        name="bg_battlefield_dawn", w=1536, h=640, steps=40, cfg=7.0,
        clip="pre-battle open hillside at dawn, golden hour light, stone fortification tiles, pale blue mist valley",
        full=(
            "vast open hillside battlefield at early dawn, clean pale stone fortification tiles "
            "extending wide across the foreground, dew drops catching the first light, long cool "
            "blue morning shadows stretching across the tactical grid, rolling misty valley visible "
            "in the deep background, a warm amber-to-periwinkle sky gradient from horizon to zenith, "
            "thin wispy ground mist hugging the terrain, ancient stone boundary walls and low "
            "embankments at the edges, an atmosphere of tense pre-battle quiet, serene yet charged, "
            "no characters, wide painterly cinematic overhead composition, 8k"
        ),
        neg=NEG + ", dark night, neon glow, industrial ruins, sci-fi technology, overcast grey, rain, mud, cartoon, anime, medieval fantasy, people",
    ),

    # ── Gem / crystal icons 256x256 ───────────────────────────────────────────
    dict(
        name="gem_energy", w=512, h=512, steps=28, cfg=8.5,
        clip="glowing energy crystal gem, electric yellow-white, faceted, floating on black",
        full=(
            "single glowing energy crystal gem, electric yellow-white inner light, "
            "faceted crystalline surface, floating isolated on pure black, "
            "game icon art, hyper detailed, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="gem_plasma", w=512, h=512, steps=28, cfg=8.5,
        clip="glowing plasma crystal gem, hot magenta-pink, fluid swirl inside, on black",
        full=(
            "single glowing plasma crystal gem, hot magenta-pink fluid plasma swirl inside, "
            "faceted crystalline surface, floating isolated on pure black, "
            "game icon art, hyper detailed, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="gem_nano", w=512, h=512, steps=28, cfg=8.5,
        clip="glowing nano-tech crystal gem, bright cyan-teal, circuit pattern, on black",
        full=(
            "single glowing nano-tech crystal gem, bright cyan-teal, circuit pattern inside facets, "
            "floating isolated on pure black, game icon art, hyper detailed, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="gem_quantum", w=512, h=512, steps=28, cfg=8.5,
        clip="quantum crystal gem, deep violet-purple, impossible geometry inside, on black",
        full=(
            "single quantum energy crystal gem, deep violet-purple, impossible fractal geometry inside, "
            "floating isolated on pure black, game icon art, hyper detailed, 8k"
        ),
        neg=NEG_SPRITE,
    ),
    dict(
        name="gem_data", w=512, h=512, steps=28, cfg=8.5,
        clip="data crystal gem, bright matrix green, binary streams inside, on black",
        full=(
            "single data crystal gem, bright matrix-code green, binary light streams inside facets, "
            "floating isolated on pure black, game icon art, hyper detailed, 8k"
        ),
        neg=NEG_SPRITE,
    ),
]

# ── DML pipeline (ported directly from Chronicle Forge image_gen.py) ──────────

_pipeline   = None
_dml_device = None


def _patch_dml_unet(unet, dml_device):
    """
    CPU->DML->CPU bridge for the SDXL UNet.
    Diffusers schedules latents on CPU; UNet lives on DML.
    Without this patch every DML op crashes with 'unbox expects Dml tensor'.
    Patches 1-3 fix internal CPU tensors produced inside UNet forward.
    """
    import torch as _t

    # Patch 0: bridge
    orig_fwd = unet.forward

    def _bridged(sample, timestep, encoder_hidden_states, *args, **kwargs):
        sample = sample.to(dml_device)
        if encoder_hidden_states is not None:
            encoder_hidden_states = encoder_hidden_states.to(dml_device)
        if kwargs.get("added_cond_kwargs"):
            kwargs["added_cond_kwargs"] = {
                k: v.to(dml_device) if isinstance(v, _t.Tensor) else v
                for k, v in kwargs["added_cond_kwargs"].items()
            }
        result = orig_fwd(sample, timestep, encoder_hidden_states, *args, **kwargs)
        if isinstance(result, tuple):
            return tuple(x.to("cpu") if isinstance(x, _t.Tensor) else x for x in result)
        if isinstance(result, _t.Tensor):
            return result.to("cpu")
        return result

    unet.forward = _bridged

    # Patch 1: time_proj (int64 -- DML can't handle it)
    orig_tp = unet.time_proj.forward
    unet.time_proj.forward = lambda t: orig_tp(t.cpu())

    # Patch 2: time_embedding (receives CPU float -- needs DML)
    orig_te = unet.time_embedding.forward
    unet.time_embedding.forward = lambda *a, **kw: orig_te(a[0].to(dml_device), *a[1:], **kw)

    # Patch 3: SDXL add_embedding
    if hasattr(unet, "add_embedding"):
        orig_ae = unet.add_embedding.forward
        unet.add_embedding.forward = lambda *a, **kw: orig_ae(a[0].to(dml_device), *a[1:], **kw)


def _load_pipeline():
    global _pipeline, _dml_device
    if _pipeline is not None:
        return _pipeline

    try:
        import torch
        import torch_directml
        from diffusers import DPMSolverMultistepScheduler, StableDiffusionXLPipeline
    except ImportError as exc:
        raise RuntimeError(
            f"Missing package: {exc}\n"
            "Run: pip install torch-directml diffusers transformers accelerate safetensors"
        ) from exc

    if not os.path.isdir(MODEL_PATH):
        raise FileNotFoundError(f"Model not found: {MODEL_PATH}")

    import torch
    import torch_directml
    from diffusers import DPMSolverMultistepScheduler, StableDiffusionXLPipeline

    print("  Loading SDXL (UNet->DirectML | VAE->CPU)...", end=" ", flush=True)
    t0 = time.time()

    dml  = torch_directml.device()
    pipe = StableDiffusionXLPipeline.from_pretrained(
        MODEL_PATH,
        torch_dtype=torch.float16,
        variant="fp16",
        use_safetensors=True,
    )
    pipe.scheduler = DPMSolverMultistepScheduler.from_config(
        pipe.scheduler.config,
        use_karras_sigmas=True,
        algorithm_type="dpmsolver++",
    )
    pipe.unet.to(dml)
    pipe.vae.to(torch.float32)
    pipe.enable_attention_slicing(1)
    _patch_dml_unet(pipe.unet, dml)

    # Force latents onto CPU -- avoids scheduler device mismatch
    _orig_prepare = pipe.prepare_latents

    def _cpu_latents(batch_size, num_channels, height, width, dtype, device, generator, latents=None):
        shape = (
            batch_size, num_channels,
            int(height) // pipe.vae_scale_factor,
            int(width)  // pipe.vae_scale_factor,
        )
        if latents is None:
            latents = torch.randn(shape, generator=generator, dtype=dtype)
        return latents * pipe.scheduler.init_noise_sigma

    pipe.prepare_latents = _cpu_latents

    _dml_device = dml
    _pipeline   = pipe
    print(f"done ({time.time()-t0:.0f}s)")
    return pipe


def _trim_clip(pipe, text, limit=74):
    ids = pipe.tokenizer.encode(text)
    return text if len(ids) <= limit else pipe.tokenizer.decode(ids[:limit], skip_special_tokens=True)


def _trim_openclip(pipe, text, limit=74):
    ids = pipe.tokenizer_2.encode(text)
    return text if len(ids) <= limit else pipe.tokenizer_2.decode(ids[:limit], skip_special_tokens=True)


def _decode_on_cpu(pipe, latents):
    """VAE decode on CPU -- avoids VRAM OOM from the large scratch buffer."""
    from PIL import Image as PILImage
    import torch
    latents_cpu = latents.to("cpu").float()
    with torch.no_grad():
        decoded = pipe.vae.decode(latents_cpu / pipe.vae.config.scaling_factor).sample
    decoded = (decoded / 2 + 0.5).clamp(0, 1)
    arr     = decoded.squeeze(0).permute(1, 2, 0).numpy()
    arr     = (arr * 255).round().astype(np.uint8)
    return PILImage.fromarray(arr)


def generate_asset(pipe, asset):
    import torch
    cpu  = torch.device("cpu")
    seed = random.randint(0, 2**31 - 1)

    clip_prompt = _trim_clip(pipe, asset["clip"])
    openclip    = _trim_openclip(pipe, f"{asset['clip']}, {asset['full']}")
    neg         = asset.get("neg", NEG)

    print(f"    encode...", end=" ", flush=True)
    t0 = time.time()

    generator = torch.Generator("cpu").manual_seed(seed)

    (prompt_emb, neg_emb,
     pooled_emb, neg_pooled_emb) = pipe.encode_prompt(
        prompt=clip_prompt,
        prompt_2=openclip,
        device=cpu,
        num_images_per_prompt=1,
        do_classifier_free_guidance=True,
        negative_prompt=neg,
        negative_prompt_2=neg,
    )

    print(f"done ({time.time()-t0:.0f}s) diffuse...", end=" ", flush=True)
    t0 = time.time()

    pipe_kwargs = dict(
        prompt_embeds=prompt_emb,
        negative_prompt_embeds=neg_emb,
        pooled_prompt_embeds=pooled_emb,
        negative_pooled_prompt_embeds=neg_pooled_emb,
        width=asset["w"],
        height=asset["h"],
        num_inference_steps=asset["steps"],
        guidance_scale=asset["cfg"],
        generator=generator,
        output_type="latent",
    )

    for attempt in range(2):
        try:
            out = pipe(**pipe_kwargs)
            break
        except RuntimeError as exc:
            if attempt == 0:
                print(f"\n    DML error -- flushing and retrying ({exc})...")
                gc.collect()
            else:
                raise

    print(f"done ({time.time()-t0:.0f}s) decode...", end=" ", flush=True)
    t0 = time.time()

    image = _decode_on_cpu(pipe, out.images)

    del out, prompt_emb, neg_emb, pooled_emb, neg_pooled_emb
    gc.collect()

    print(f"done ({time.time()-t0:.0f}s)")
    return image


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    regen   = "--regen"   in sys.argv
    dry_run = "--dry-run" in sys.argv
    bg_only = "--bg-only" in sys.argv

    os.makedirs(OUT_DIR, exist_ok=True)

    todo = []
    for a in ASSETS:
        if bg_only and not a["name"].startswith("bg_"):
            continue
        dest = os.path.join(OUT_DIR, f"{a['name']}.png")
        if not regen and os.path.exists(dest) and os.path.getsize(dest) > 1024:
            print(f"  skip  {a['name']}.png  (exists)")
        else:
            todo.append((a, dest))

    print(f"\n{len(todo)}/{len(ASSETS)} assets to generate -> {OUT_DIR}\n")

    if dry_run:
        for a, _ in todo:
            print(f"  [{a['name']}]  {a['w']}x{a['h']}  steps={a['steps']}  cfg={a['cfg']}")
            print(f"    CLIP:     {a['clip']}")
            print(f"    OpenCLIP: {a['full'][:80]}...")
        return

    if not todo:
        print("All assets already exist. Pass --regen to regenerate.")
        return

    pipe = _load_pipeline()

    for i, (a, dest) in enumerate(todo, 1):
        print(f"[{i}/{len(todo)}] {a['name']}  ({a['w']}x{a['h']})")
        try:
            image = generate_asset(pipe, a)
            try:
                image.save(dest)
            except KeyboardInterrupt:
                if os.path.exists(dest):
                    os.remove(dest)
                raise
            print(f"    saved -> {dest}")
        except KeyboardInterrupt:
            print("\nInterrupted. Partially generated files removed.")
            sys.exit(0)
        except Exception as exc:
            print(f"    ERROR: {exc}")
            continue

    print(f"\nDone. Check {OUT_DIR}")


if __name__ == "__main__":
    main()
