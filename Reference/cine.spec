# -*- mode: python ; coding: utf-8 -*-
import os
import sys
import shutil
import subprocess

if os.name == 'nt':
    msys_path = "C:\\msys64\\mingw64\\bin"
    os.environ["PATH"] = msys_path + os.pathsep + os.environ["PATH"]
    if hasattr(os, 'add_dll_directory'):
        os.add_dll_directory(msys_path)

from PyInstaller.utils.hooks import collect_data_files, collect_submodules

block_cipher = None

project_root = os.path.abspath(os.getcwd())

def compile_resources():
    src_dir = os.path.join(project_root, "src")
    data_dir = os.path.join(project_root, "data")

    blueprints = [f for f in os.listdir(src_dir) if f.endswith(".blp")]
    blueprint_compiler = shutil.which("blueprint-compiler")
    missing_ui = []
    for blp in blueprints:
        ui_file = os.path.join(src_dir, blp.replace(".blp", ".ui"))
        if not os.path.exists(ui_file):
            missing_ui.append(blp)

    if missing_ui:
        if not blueprint_compiler:
            raise SystemExit(
                "Missing .ui files and blueprint-compiler is not available."
            )
        for blp in missing_ui:
            ui_file = os.path.join(src_dir, blp.replace(".blp", ".ui"))
            blp_file = os.path.join(src_dir, blp)
            subprocess.check_call(
                [blueprint_compiler, "compile", "--output", ui_file, blp_file]
            )

    gresource_xml = os.path.join(src_dir, "cine.gresource.xml")
    gresource_bin = os.path.join(src_dir, "cine.gresource")
    glib_compile = shutil.which("glib-compile-resources")
    if not os.path.exists(gresource_bin):
        if not glib_compile:
            raise SystemExit(
                "cine.gresource is missing and glib-compile-resources is not available."
            )
        subprocess.check_call(
            [glib_compile, "--target", gresource_bin, gresource_xml], cwd=src_dir
        )

    glib_compile_schemas = shutil.which("glib-compile-schemas")
    compiled_schemas = os.path.join(data_dir, "gschemas.compiled")
    if not os.path.exists(compiled_schemas):
        if not glib_compile_schemas:
            raise SystemExit(
                "gschemas.compiled is missing and glib-compile-schemas is not available."
            )
        subprocess.check_call([glib_compile_schemas, data_dir])

def find_mpv_binaries():
    if sys.platform != "win32":
        return []
    candidates = []
    env_keys = ["MPV_HOME", "MPV_DIR", "MPV_PATH"]
    for key in env_keys:
        val = os.environ.get(key)
        if val:
            candidates.append(val)
    for entry in os.environ.get("PATH", "").split(os.pathsep):
        if entry:
            candidates.append(entry)
    dll_names = ["mpv-1.dll", "libmpv-2.dll"]
    for base in candidates:
        for dll in dll_names:
            dll_path = os.path.join(base, dll)
            if os.path.exists(dll_path):
                return [(dll_path, ".")]
    return []

compile_resources()

hiddenimports = collect_submodules('src') + [
    'gi.repository',
    'gi.repository.Adw',
    'gi.repository.Gio',
    'gi.repository.GLib',
    'gi.repository.GObject',
    'gi.repository.Gtk',
    'gi.repository.Gdk',
    'gi.repository.GdkPixbuf',
    'gi.repository.Pango',
    'gi.repository.PangoCairo',
    'gi.repository.cairo',
    'gi.overrides',
    'gi.overrides.Gtk',
    'gi.overrides.Gio',
    'gi.overrides.GLib',
    'gi.overrides.GObject',
    'gi.overrides.Pango',
]

datas = [
    ('src/cine.gresource', '.'),
    ('src/style.css', 'src'),
    ('data/icons', 'data/icons'),
    ('src/*.ui', 'src'), 
    ('data/gschemas.compiled', 'share/glib-2.0/schemas'), # Bundle compiled schemas
    ('data/io.github.diegopvlk.Cine.gschema.xml', 'share/glib-2.0/schemas'), # Bundle source xml just in case
    # Bundle GTK typelibs
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Gtk-4.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Adw-1.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Gio-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\GioWin32-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\GLib-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\GObject-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Gdk-4.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\GdkPixbuf-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Pango-1.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\PangoCairo-1.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\PangoFc-1.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\PangoFT2-1.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\HarfBuzz-0.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\cairo-1.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Graphene-1.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\Gsk-4.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\freetype2-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\fontconfig-2.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\GdkWin32-4.0.typelib', 'girepository-1.0'),
    ('C:\\msys64\\mingw64\\lib\\girepository-1.0\\GModule-2.0.typelib', 'girepository-1.0'),
]

# If we have icons in the gresource, we might not need to bundle them separately,
# but it doesn't hurt.

mpv_binaries = find_mpv_binaries()

a = Analysis(
    ['run.py'],
    pathex=[project_root, "C:\\msys64\\mingw64\\bin"],
    binaries=mpv_binaries,
    datas=datas,
    hiddenimports=hiddenimports + ['gi', 'gi.repository.Adw', 'gi.repository.Gtk', 'gi.repository.Gio', 'gi.repository.GLib'],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=['pyi_rth_gi.py'],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

# PyInstaller hook for GObject Introspection
# This usually requires a hook to handle typelibs.
# On Windows, we often need to manually point to the typelib directory.

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

icon_path = os.path.join(project_root, "Cine.ico")
icon_value = icon_path if os.path.exists(icon_path) else None

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='Cine',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=icon_value
)
