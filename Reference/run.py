
import os
import sys
import subprocess

def main():
    # Check if running as PyInstaller frozen exe
    is_frozen = getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS')
    
    # Set the working directory to the script's directory
    if is_frozen:
        # When frozen, use the temporary directory PyInstaller creates
        base_path = sys._MEIPASS
    else:
        os.chdir(os.path.dirname(os.path.abspath(__file__)))
        base_path = os.path.dirname(os.path.abspath(__file__))
    
    # Only check for MSYS2 if NOT running as frozen exe
    if not is_frozen:
        # Check if we are running in MSYS2 environment
        is_msys2 = "MSYSTEM" in os.environ
        
        if os.name == "nt" and not is_msys2:
            # Check for MSYS2 Python
            msys2_python = r"C:\msys64\mingw64\bin\python.exe"
            if os.path.exists(msys2_python):
                print(f"Detected MSYS2 Python at {msys2_python}. Re-launching...")
                # Re-launch using MSYS2 Python
                cmd = [msys2_python, "-m", "src.main"] + sys.argv[1:]
                subprocess.run(cmd)
                return

    # If we are already in MSYS2 or running as frozen exe
    print("Starting Cine...")
    from src.main import main as app_main
    sys.exit(app_main(sys.argv))

if __name__ == "__main__":
    main()
