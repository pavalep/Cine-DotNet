# main.py
#
# Copyright 2026 Diego Povliuk
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.
#
# SPDX-License-Identifier: GPL-3.0-or-later

import os
import sys

# Set up environment for PyInstaller bundle BEFORE importing gi
if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
    base_path = sys._MEIPASS
    # Set GSettings schema directory
    schema_path = os.path.join(base_path, "share", "glib-2.0", "schemas")
    os.environ["GSETTINGS_SCHEMA_DIR"] = schema_path
    
    # Also set XDG_DATA_DIRS as a fallback
    xdg_data = os.path.join(base_path, "share")
    os.environ["XDG_DATA_DIRS"] = xdg_data + os.pathsep + os.environ.get("XDG_DATA_DIRS", "")
    
    # Set GI typelib path
    typelib_path = os.path.join(base_path, "girepository-1.0")
    os.environ["GI_TYPELIB_PATH"] = typelib_path

import gi
import subprocess
from typing import cast
from gettext import gettext as _

gi.require_version("Adw", "1")
gi.require_version("Gio", "2.0")
gi.require_version("GLib", "2.0")
gi.require_version("Gtk", "4.0")
from gi.repository import Adw, Gio, GLib, Gtk

def register_resources():
    """Register GResources and GSettings schemas for the application."""
    try:
        # PyInstaller support: check for _MEIPASS
        if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
            base_path = sys._MEIPASS
            # Environment already set before gi import
        else:
            # Running from source
            src_path = os.path.dirname(os.path.abspath(__file__))
            project_root = os.path.dirname(src_path)
            base_path = src_path
            
            # Set GSettings schema directory for local development
            schema_path = os.path.join(project_root, "data")
            if os.path.exists(os.path.join(schema_path, "gschemas.compiled")):
                os.environ["GSETTINGS_SCHEMA_DIR"] = schema_path
            
        # Try several locations for the gresource file
        locations = [
            os.path.join(base_path, "cine.gresource"),
            os.path.join(base_path, "src", "cine.gresource"),
            os.path.join(os.path.dirname(base_path), "cine.gresource"),
        ]
        
        resource_path = None
        for loc in locations:
            if os.path.exists(loc):
                resource_path = loc
                break

        if resource_path:
            resource = Gio.Resource.load(resource_path)
            resource._register()
            return True
        else:
            print("Warning: cine.gresource not found.")
    except Exception as e:
        print(f"Failed to register resources: {e}")
    return False

# Register resources before importing local modules that might use them
register_resources()

from .window import CineWindow
from .preferences import Preferences, settings
from .mpris import MPRIS

# Set the icon shown in gnome sound settings
os.environ["PIPEWIRE_PROPS"] = '{application.icon-name="io.github.diegopvlk.Cine"}'

class CineApplication(Adw.Application):
    """The main application singleton class."""

    def __init__(self, version="1.0.7"):
        super().__init__(
            application_id="io.github.diegopvlk.Cine",
            flags=Gio.ApplicationFlags.HANDLES_OPEN,
            resource_base_path="/io/github/diegopvlk/Cine",
        )
        self.version = version

        self.add_main_option(
            "new-window",
            ord("n"),
            GLib.OptionFlags.NONE,
            GLib.OptionArg.NONE,
            "Open a new window",
            None,
        )

        self.connect("window-removed", self._on_window_removed)

    def do_startup(self):
        if os.name != "nt":
            MPRIS(self)

        Adw.Application.do_startup(self)
        Adw.StyleManager.get_default().props.color_scheme = Adw.ColorScheme.FORCE_DARK

        self._create_action("new-window", lambda *a: self.activate(), ["<primary>n"])
        self._create_action("quit", lambda *a: self.quit(), ["<primary>q"])
        self._create_action("about", self._on_about_action)
        self._create_action(
            "preferences", self.on_preferences_action, ["<primary>comma"]
        )

    def do_activate(self):
        win = CineWindow(application=self)
        win.present()

    def do_open(  # pyright: ignore[reportIncompatibleMethodOverride]
        self, gfiles, _n_files, _hint
    ):
        win: CineWindow = cast(CineWindow, self.props.active_window)
        open_new = settings.get_boolean("open-new-windows") or not win

        if open_new:
            win = CineWindow(application=self)
            win.start_page.set_visible(False)

            first_video_path = None
            for gfile in gfiles:
                first_video_path = self.find_first_file(gfile)

                if first_video_path:
                    break

            if first_video_path:
                try:
                    cmd = [
                        "ffprobe",
                        "-v",
                        "error",
                        "-select_streams",
                        "v:0",
                        "-show_entries",
                        "stream=width,height",
                        "-of",
                        "csv=s=x:p=0",
                        first_video_path,
                    ]
                    kwargs = {}
                    if os.name == "nt":
                        kwargs["creationflags"] = subprocess.CREATE_NO_WINDOW
                    output = subprocess.check_output(
                        cmd, text=True, timeout=2, stderr=subprocess.DEVNULL, **kwargs
                    ).strip()

                    if output:
                        # 1920x1080
                        res = output.splitlines()[0].split("x")
                        if len(res) >= 2:
                            win._set_window_size(int(res[0]), int(res[1]))
                except Exception as e:
                    print(f"Metadata probe skipped or failed: {e}")
            win.present()
        else:
            win.present()
            win.mpv.stop()

        for gfile in gfiles:
            path = gfile.get_path() or gfile.get_uri()
            if path:
                win.mpv.loadfile(path, "append-play")

        for window in self.get_windows():
            w = cast(CineWindow, window)
            # Pause previous opened windows
            w.mpv.pause = w != win

        win._hide_ui_timeout()

    def find_first_file(self, gfile, visited=None):
        """Local-only recursive search."""
        if gfile.get_uri_scheme() != "file":
            return None

        if visited is None:
            visited = set()

        path = gfile.get_path()
        if not path or path in visited:
            return None
        visited.add(path)

        try:
            info = gfile.query_info(
                "standard::type", Gio.FileQueryInfoFlags.NOFOLLOW_SYMLINKS, None
            )
            f_type = info.get_file_type()

            if f_type == Gio.FileType.REGULAR:
                return path

            if f_type == Gio.FileType.DIRECTORY:
                enumerator = gfile.enumerate_children(
                    "standard::name,standard::type",
                    Gio.FileQueryInfoFlags.NOFOLLOW_SYMLINKS,
                    None,
                )

                subdirectories = []
                for child in enumerator:
                    child_type = child.get_file_type()
                    name = child.get_name()

                    if name.startswith("."):
                        continue

                    if child_type == Gio.FileType.REGULAR:
                        return gfile.get_child(name).get_path()
                    elif child_type == Gio.FileType.DIRECTORY:
                        subdirectories.append(gfile.get_child(name))

                for folder in subdirectories:
                    found = self.find_first_file(folder, visited)
                    if found:
                        return found
        except Exception:
            pass
        return None

    # From showtime
    def do_handle_local_options(self, options: GLib.VariantDict):
        """Handle local command line arguments."""
        self.register()  # This is so props.is_remote works

        if self.props.is_remote:
            if options.contains("new-window"):
                return -1

            print("Cine is runnning, to open a new window, run with --new-window.")
            return 0

        return -1

    def on_preferences_action(self, *args):
        """Callback for the app.preferences action."""
        preferences = Preferences(self.props.active_window)
        preferences.present(self.props.active_window)

    def _on_about_action(self, *args):
        """Callback for the app.about action."""
        about = Adw.AboutDialog(
            application_name=_("Cine"),
            application_icon="io.github.diegopvlk.Cine",
            developer_name="Diego Povliuk",
            version=self.version,
            copyright="© 2026 Diego Povliuk",
            issue_url="https://github.com/diegopvlk/Cine/issues",
            license_type=Gtk.License.GPL_3_0,
        )
        try:
            # Translators: Replace "translator-credits" with your name/username, and optionally an email or URL.
            about.set_translator_credits(_("translator-credits"))
        except NameError:
            pass

        about.add_acknowledgement_section(
            None,
            [
                "MPV https://mpv.io/",
                "python-mpv https://pypi.org/project/python-mpv/",
                "Celluloid https://celluloid-player.github.io/",
                "Showtime https://apps.gnome.org/Showtime/",
                "Workbench https://apps.gnome.org/Workbench/",
            ],
        )

        about.add_link(
            "Donate (PayPal)",
            "https://www.paypal.com/donate?hosted_button_id=DVL7H35GA66X6",
        )

        about.add_link(
            "Doar (Pix): diego.pvlk@gmail.com",
            "mailto:diego.pvlk@gmail.com",
        )

        about.add_other_app(
            "io.github.diegopvlk.Dosage", "Dosage", "Keep track of your treatments"
        )

        about.add_other_app(
            "io.github.diegopvlk.Tomatillo", "Tomatillo", "Focus better, work smarter"
        )

        about.present(self.props.active_window)

    def _create_action(self, name, callback, shortcuts=None):
        """Add an application action."""
        action = Gio.SimpleAction.new(name, None)
        action.connect("activate", callback)
        self.add_action(action)
        if shortcuts:
            self.set_accels_for_action(f"app.{name}", shortcuts)

    def _on_window_removed(self, _obj, win):
        win.mpv.quit()


def main(version="1.0.7"):
    """The application's entry point."""
    # Set locale to C to avoid mpv locale warnings on Windows
    if os.name == "nt":
        import locale
        try:
            locale.setlocale(locale.LC_NUMERIC, "C")
        except Exception:
            print("Warning: Could not set LC_NUMERIC to C locale")
    
    app = CineApplication(version=version)
    return app.run(sys.argv)

if __name__ == "__main__":
    main()
