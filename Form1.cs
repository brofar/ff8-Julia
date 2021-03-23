using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Timer = System.Threading.Timer;
using ProcessMemoryReaderLib;

namespace ff8_Julia
{
    public partial class formMain : Form
    {
        // Declare some global variables.
        static Process FF8Process;
        static string GameVersion;
        static IntPtr GameBaseAddress;
        static int carawayMapId = 761;
        static int carawayMoment = 351;
        static string carawayCode;
        static Timer t;

        static Dictionary<string, int> CodeAddress = new Dictionary<string, int>
        {
            { "EN", 0x18FEC90 },
            { "FR", 0x18FE968 }
        };

        static Dictionary<string, int> MapIdAddress = new Dictionary<string, int>
        {
            { "EN", 0x18D2FC0 },
            { "FR", 0x18D2C98 }
        };

        static Dictionary<string, int> MomentAddress = new Dictionary<string, int>
        {
            { "EN", 0x18FEAB8 },
            { "FR", 0x18FE790 }
        };

        public formMain()
        {
            InitializeComponent();
        }

        // Thread-Safe Call to Windows Forms Control
        delegate void SetCodeTextCallback(string text);
        delegate void SetStatusTextCallback(string text);
        public void SetCodeText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.lblCode.InvokeRequired)
            {
                SetCodeTextCallback d = new SetCodeTextCallback(SetCodeText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.lblCode.Text = text;
            }
        }
        public void SetStatusText(string text)
        {
            if (this.lblStatus.InvokeRequired)
            {
                SetStatusTextCallback d = new SetStatusTextCallback(SetStatusText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.lblStatus.Text = text;
            }
        }


        private void BeginCodeProcessing()
        {
            // Detect the game
            SetCodeText("0");
            SetStatusText("Waiting for game to start.");
            DetectGame();
        }



        private bool CodeCriteria()
        {
            bool goMode = false;

            do
            {
                // Story Moment
                var momentAddress = MomentAddress[GameVersion];
                var gameMoment = ReadMemoryAddress(momentAddress, 2);

                // Map ID
                var mapIdAddress = MapIdAddress[GameVersion];
                var mapId = ReadMemoryAddress(mapIdAddress, 2);

                bool rightMap = (mapId == carawayMapId);
                bool rightMoment = (gameMoment >= carawayMoment);

                goMode = (rightMap && rightMoment);
            } while (goMode == false);

            return goMode;
        }
        private async void DetectGame()
        {
            // Find the FF8 process.
            Console.WriteLine("Checking for game...");
            Process ff8Game = await Task.Run(FindGame);
            Console.WriteLine("Game found.");

            // Get the language from the process name (i.e. remove "FF8_" from the name)
            GameVersion = ff8Game.ProcessName.Substring(4);
            GameBaseAddress = ff8Game.MainModule.BaseAddress;

            // Add event handler for exited process
            ff8Game.EnableRaisingEvents = true;
            ff8Game.Exited += new EventHandler(myprc_Exited);

            FF8Process = ff8Game;

            // Update status
            SetStatusText(GameVersion + " - Waiting for Caraway's Mansion scene in game.");

            bool codeCriteria = await Task.Run(CodeCriteria);


            t = new Timer(SearchForCode, null, 0, 500);
            //GetCode();

        }

        private void SearchForCode(object state)
        {
            // Story Moment
            var momentAddress = MomentAddress[GameVersion];
            var gameMoment = ReadMemoryAddress(momentAddress, 2);

            // Map ID
            var mapIdAddress = MapIdAddress[GameVersion];
            var mapId = ReadMemoryAddress(mapIdAddress, 2);

            bool rightMap = (mapId == carawayMapId);
            bool rightMoment = (gameMoment >= carawayMoment);

            if(rightMap && rightMoment)
            {
                GetCode();
            }
        }

        private void GetCode()
        {
            var codeAddress = CodeAddress[GameVersion];
            var code = ReadMemoryAddress(codeAddress, 1);
            var codeString = code.ToString().PadLeft(3, '0');

            if(carawayCode != codeString)
            {
                carawayCode = codeString;
                SetCodeText(codeString);
                SetStatusText("Mansion Code found.");
            }
        }

        private Process FindGame()
        {
            List<Process> processes = new List<Process>();
            do
            {
                processes = Process.GetProcesses()
                .Where(x => (x.ProcessName.StartsWith("FF8_", StringComparison.OrdinalIgnoreCase))
                            && !(x.ProcessName.Equals("FF8_Launcher", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            } while (processes.Count == 0);

            return processes[0];
        }

        private int ReadMemoryAddress(int offset, uint bytelength)
        {
            int bytesReadSize;

            ProcessMemoryReader reader = new ProcessMemoryReader();

            reader.ReadProcess = FF8Process;
            reader.OpenProcess();

            IntPtr readAddress = IntPtr.Add(GameBaseAddress, offset);
            byte[] mem = reader.ReadProcessMemory(readAddress, bytelength, out bytesReadSize);

            int i = ByteToInt(mem, bytesReadSize);

            return i;
        }

        private int ByteToInt(byte[] bytes, int size)
        {
            int i = 0;
            try
            {
                if (size == 4)
                {
                    i = BitConverter.ToInt32(bytes, 0);
                }
                else if (size == 2)
                {
                    i = BitConverter.ToInt16(bytes, 0);
                }
                else if (size == 1)
                {
                    i = bytes[0];
                }
            }
            catch (Exception e)
            {
                SetStatusText("Program error. ByteToInt issue.");
                Console.WriteLine(e.Message);
            }
            return i;
        }



        private void myprc_Exited(object sender, EventArgs e)
        {
            // Kill the timer if it exists already
            if (t != null)
                t.Dispose();

            BeginCodeProcessing();
        }

        private void formMain_Load(object sender, EventArgs e)
        {
            BeginCodeProcessing();
        }
    }
}
