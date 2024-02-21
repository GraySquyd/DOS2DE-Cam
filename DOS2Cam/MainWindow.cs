using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using SharpDX.XInput;
using System.Windows.Forms;
using System.Linq;
using System;

namespace DOS2Cam
{
    public partial class MainWindow : Form
    {
        Memory mem;
        public MainWindow()
        {
            InitializeComponent();
        }
        nint obj;
        int battleCamOffset;
        int worldCamOffset;
        int camMaxOffset;
        int camMaxAbsOffset;
        int camTiltOffset;
        int camTiltSpeedOffset;
        int combatZoomOutAddr;
        int combatZoomInAddr;
        Dictionary<nint, float> defaultVals = new Dictionary<nint, float>();
        int prevMouseY;
        float curTilt;

        Controller controller;
        Gamepad gamepad;

        //get addresses
        void MainWindow_Load(object sender, EventArgs e)
        {

            
            controller = new Controller(UserIndex.One);

            var DOS2Proc = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == "EoCApp" || p.ProcessName == "EoCApp");
            //needs changing since BG3 used two different process names for different graphics apis
            if (DOS2Proc == null)
            {
                MessageBox.Show("Divinity 2 not found", "DOS2 not found");
                Environment.Exit(0);
            }
            mem = new Memory(DOS2Proc);

            IntPtr processHandle = mem.GetProcessHandle();

            if (processHandle != IntPtr.Zero)
            {
                IntPtr baseAddress = DOS2Proc.MainModule.BaseAddress;
                IntPtr finalAddress = IntPtr.Add(baseAddress, 2959898 + 0xC40);
                Debug.WriteLine($"Base Address of the process: 0x{baseAddress.ToInt64():X}");
                Debug.WriteLine($"Final Address after adding 2959898 and offset C40: 0x{finalAddress.ToInt64():X}");
                CloseHandle(processHandle);
            }
            else
            {
                Console.WriteLine("Failed to get process handle from Memory class.");
            }


            //need to use finalAddress (minus the C40)
            //var maxDist = IntPtr.Add(baseAddress, 2959898); ??? maybe


            var camFuncAddr = mem.FindPattern("48 8B 05 81 FB 6F 02 C3");
            //var camFuncAddr = mem.FindPattern("D0 E1 33 5D B3 01 00 00");
            var objBase = mem.ReadProcessMemory<int>(camFuncAddr);
            //var objBase = mem.ReadProcessMemory<int>(camFuncAddr + 3);
            //obj = mem.ReadProcessMemory<nint>(camFuncAddr + 3 + 4 + objBase);
            obj = mem.ReadProcessMemory<nint>(camFuncAddr - 3072);

            var camFuncBytes = mem.ReadProcessMemory(camFuncAddr, 0x100);

            var battleCamOffsetStart = mem.FindPattern("49 8D 80 ? ? ? ? F6 C1 01", bytes: camFuncBytes);
            battleCamOffset = BitConverter.ToInt32(camFuncBytes, (int)battleCamOffsetStart + 3);

            var worldCamOffsetStart = mem.FindPattern("F6 C1 01 75 07 49 8D 80", bytes: camFuncBytes);
            worldCamOffset = BitConverter.ToInt32(camFuncBytes, (int)worldCamOffsetStart + 8);

            var maxDistOffAddr = mem.FindPattern("C3 F3 0F 10 48", bytes: camFuncBytes);
            camMaxOffset = camFuncBytes[maxDistOffAddr + 5];
            
            var camMaxAbs = mem.FindPattern("F3 0F 10 80 ? ? ? ? C3", bytes: camFuncBytes);
            camMaxAbsOffset = camFuncBytes[camMaxAbs + 4];

            var camTilt = mem.FindPattern("C3 F3 0F 10 80 ? ? ? ? F3 0F 10 88 ? ? ? ? 0F 14 C8 66 48 0F 7E C8 C3");
            camTiltOffset = mem.ReadProcessMemory<int>(camTilt + 5);

            var combatZoomOut = mem.FindPattern("F3 45 0F 11 4C 24 5C");
            combatZoomOutAddr = mem.ReadProcessMemory<int>(combatZoomOut);

            var combatZoomIn = mem.FindPattern("F3 0F 11 70 5C 0F 28 74 24 20");
            combatZoomOutAddr = mem.ReadProcessMemory<int>(combatZoomIn);

            
            var curMaxZoom = AddDefaultVal(obj + worldCamOffset + camMaxOffset); // default val is 12, addr offset is 0x7b4 in release patch
            if (curMaxZoom != 12)
            {
                var res = MessageBox.Show("Max Zoom not found, expected 12.0, found : " + curMaxZoom, "Bad Game State", MessageBoxButtons.AbortRetryIgnore);
                if (res == DialogResult.Abort)
                    Environment.Exit(0);
            }
            

            maxZoomVal.Value = (decimal)24f;
            minZoomVal.Value = (decimal)0.5f;
            panSpeedVal.Value = (decimal)100f;
            fovVal.Value = (decimal)65f; //do both fovs + combat fovs
            scrollSpeedVal.Value = (decimal)0.8f;
            cameraDistanceVal.Value = (decimal)100f;
            cameraHeightVal.Value = (decimal)0.00085d;
            tacticalZoomMaxVal.Value = (decimal)100f;
            tacticalZoomMinVal.Value = (decimal)10f;

            
            curTilt = AddDefaultVal(obj + worldCamOffset + camTiltOffset);
            AddDefaultVal(obj + worldCamOffset + camTiltOffset);
            AddDefaultVal(obj + worldCamOffset + camTiltOffset + 4);
            AddDefaultVal(obj + worldCamOffset + camTiltOffset + 8);
            AddDefaultVal(obj + worldCamOffset + camTiltOffset + 12);
            AddDefaultVal(obj + battleCamOffset + camTiltOffset);
            AddDefaultVal(obj + battleCamOffset + camTiltOffset + 4);
            AddDefaultVal(obj + battleCamOffset + camTiltOffset + 8);
            AddDefaultVal(obj + battleCamOffset + camTiltOffset + 12);

            AddDefaultVal(obj + worldCamOffset + camMaxOffset);
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 60); // cam height
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 92); //fov close
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 96); //fov far
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 132); //zoom steps
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 136); //scroll speed
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 160); //tact min
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 164); //tact max
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 172); //cam distance
            AddDefaultVal(obj + worldCamOffset + camMaxOffset + 200); //tilt speed2

            //combat offsets, done at the same time as the regular ones
            AddDefaultVal(obj + battleCamOffset + camMaxOffset);
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 4);
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 60); // cam height
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 92); //fov close
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 96); //fov far
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 132); //zoom steps
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 136); //scroll speed
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 160); //tact min
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 164); //tact max
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 172); //cam distance
            AddDefaultVal(obj + battleCamOffset + camMaxOffset + 200); //tilt speed

            mem.WriteProcessMemory(obj + worldCamOffset + camTiltOffset, curTilt);
            mem.WriteProcessMemory(obj + worldCamOffset + camTiltOffset + 4, curTilt);
            ChangePitchOnMouseMove();

        //Debug.WriteLine($"Camera Tilt Speed: {camTiltSpeedOffset}");
        //mem.WriteProcessMemory(obj + worldCamOffset + camTiltSpeedOffset + 240, (float)panSpeedVal.Value);
        Debug.WriteLine($"1: {AddDefaultVal(obj)}");
        Debug.WriteLine($"2: {AddDefaultVal(obj + worldCamOffset)}");
        Debug.WriteLine($"3: {AddDefaultVal(obj + worldCamOffset + camMaxOffset)}");
        Debug.WriteLine($"4: {AddDefaultVal(obj + 4)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 8)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 12)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 16)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 20)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 24)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 28)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 32)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 36)}");
        /*
        Debug.WriteLine($"1: {AddDefaultVal(obj + 40)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 44)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 48)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 52)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 56)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 60)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 64)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 68)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 72)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 76)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 80)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 84)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 88)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 92)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 96)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 100)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 104)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 108)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 112)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 116)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 120)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 124)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 128)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 132)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 136)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 140)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 144)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 148)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 152)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 156)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 160)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 164)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 168)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 172)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 176)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 180)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 184)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 188)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 192)}");
        Debug.WriteLine($"1: {AddDefaultVal(obj + 196)}");
        */
        }
        
            bool running = true;
        float AddDefaultVal(nint addr)
        {
            defaultVals[addr] = mem.ReadProcessMemory<float>(addr);
            return defaultVals[addr];
        }
        void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
            foreach (var val in defaultVals) mem.WriteProcessMemory(val.Key, val.Value);
        }
        void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //AOB scan for Combat Camera Zoom
            string pattern = "F3 45 0F 11 4C 24 5C";
            string pattern2 = "F3 0F 11 70 5C";
            List<nint> foundAddresses = mem.FindPatterns(pattern);
            List<nint> foundAddresses2 = mem.FindPatterns(pattern2);

            //writing empty bytes
            if (checkBox1.CheckState == CheckState.Checked)
            {
                byte[] nopBytes = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }; // NOP instruction
                byte[] nopBytes2 = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 };
                for (int i = 0; i < 1; i++)
                {
                    nint address = foundAddresses[i];
                    Debug.WriteLine($"Pattern found at address: 0x{address.ToString("X")}");
                    ReplaceBytes(address, nopBytes);
                    nint address2 = foundAddresses2[i];
                    Debug.WriteLine($"Pattern found at address: 0x{address2.ToString("X")}");
                    ReplaceBytes(address, nopBytes2);
                }

            }
            //replacing orgignal code
            if (checkBox1.CheckState == CheckState.Unchecked)
            {
                for (int i = 0; i < 1; i++)
                {
                    byte[] original = new byte[] { 0xF3, 0x45, 0x0F, 0x11, 0x4C, 0x24, 0x5C };
                    byte[] original2 = new byte[] { 0xF3, 0x0F, 0x11, 0x70, 0x5C };
                    nint address = foundAddresses[i];
                    Debug.WriteLine($"Pattern found at address: 0x{address.ToString("X")}");
                    ReplaceBytes(address, original);
                    nint address2 = foundAddresses2[i];
                    Debug.WriteLine($"Pattern found at address: 0x{address2.ToString("X")}");
                    ReplaceBytes(address, original);
                }
            }
            /* //error message when pattern wrong or not found
            if (foundAddresses != )

            {
                var res = MessageBox.Show("Adress Not Found", MessageBoxButtons.AbortRetryIgnore);
                if (res == DialogResult.Abort)
                    Environment.Exit(0);
            }
            */
;
        }
        void ReplaceBytes(nint address, byte[] newBytes)
        {
            mem.WriteProcessMemory(address, newBytes);
        }
        void maxZoomVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset, (float)maxZoomVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset, (float)maxZoomVal.Value);
        }
        void minZoomVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 4, (float)minZoomVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset + 4, (float)minZoomVal.Value);
        }
        void panSpeedVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 200, (float)panSpeedVal.Value);
        }
        void fovVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 92, (float)fovVal.Value);
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 96, (float)fovVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset + 92, (float)fovVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset + 96, (float)fovVal.Value);
        }

        void scrollSpeedVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 136, (float)scrollSpeedVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset + 136, (float)scrollSpeedVal.Value);
        }

        void cameraDistanceVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 172, (float)cameraDistanceVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset + 172, (float)cameraDistanceVal.Value);
        }
        void cameraHeightVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxOffset + 60, (double)cameraHeightVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxOffset + 60, (double)cameraHeightVal.Value);

        }
        private void tacticalZoomVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxAbsOffset, (float)tacticalZoomMaxVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxAbsOffset, (float)tacticalZoomMaxVal.Value);
        }
        private void tacticalZoomMinVal_ValueChanged(object sender, EventArgs e)
        {
            mem.WriteProcessMemory(obj + worldCamOffset + camMaxAbsOffset - 4, (float)tacticalZoomMinVal.Value);
            mem.WriteProcessMemory(obj + battleCamOffset + camMaxAbsOffset - 4, (float)tacticalZoomMinVal.Value);
        }

        //camera tilt from mouse
        async Task ChangePitchOnMouseMove()
        {
            while (running)
            {
                var diff = 0;
                var Mouse = false;
                if (Hotkeys.SinglePress(Keys.MButton)) prevMouseY = Cursor.Position.Y;

                if (Hotkeys.IsPressed(Keys.MButton) || Hotkeys.IsPressed(Keys.R))
                {
                    diff = Cursor.Position.Y - prevMouseY;
                    Mouse = true;
                }

                if (controller.IsConnected == true && controller.GetState().Gamepad.Buttons == GamepadButtonFlags.RightThumb)
                {
                    gamepad = controller.GetState().Gamepad;
                    if (gamepad.RightThumbY > 4500 && gamepad.RightThumbY < 24000)
                    {
                        diff = 15;
                    }
                    else if (gamepad.RightThumbY < -4500 && gamepad.RightThumbY > -24000)
                    {
                        diff = -15;
                    }
                }

                if (diff != 0)
                {
                    curTilt += diff * 0.05f;
                    mem.WriteProcessMemory(obj + worldCamOffset + camTiltOffset, curTilt);
                    mem.WriteProcessMemory(obj + worldCamOffset + camTiltOffset + 4, curTilt);
                    mem.WriteProcessMemory(obj + worldCamOffset + camTiltOffset + 8, curTilt);
                    mem.WriteProcessMemory(obj + worldCamOffset + camTiltOffset + 12, curTilt);
                    mem.WriteProcessMemory(obj + battleCamOffset + camTiltOffset, curTilt);
                    mem.WriteProcessMemory(obj + battleCamOffset + camTiltOffset + 4, curTilt);
                    mem.WriteProcessMemory(obj + battleCamOffset + camTiltOffset + 8, curTilt);
                    mem.WriteProcessMemory(obj + battleCamOffset + camTiltOffset + 12, curTilt);
                    if (Mouse == false)
                    {
                        prevMouseY = (int)curTilt;
                    }
                    else
                    {
                        prevMouseY = Cursor.Position.Y;
                        Mouse = false;
                    }
                }
                await Task.Delay(16);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);
    }
}


