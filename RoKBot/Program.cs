﻿using RoKBot.Utils;
using Shark.Messenger;
using Shark.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace RoKBot
{
    class Program
    {
        static Queue<Func<bool>> CommittingRoutines = new Queue<Func<bool>>();

        static List<Func<bool>> Routines = new List<Func<bool>>(new Func<bool>[]{

            Routine.DefeatBabarians,
            Routine.DefeatBabarians,
            Routine.DefeatBabarians,
            Routine.UpgradeCity,
            Routine.CollectResources,
            Routine.GatherResources,
            Routine.AllianceTasks,
            Routine.ClaimCampaign,
            Routine.ReadMails,
            Routine.ClaimVIP,
            Routine.Recruit,
            Routine.Explore,
            Routine.TrainInfantry,
            Routine.TrainArcher,
            Routine.TrainCavalry,
            Routine.TrainSiege,
            Routine.ClaimQuests,
            Routine.Build,
            Routine.ClaimDaily,
            Routine.HealTroops,
            Routine.Research
        });

        static DateTime RoutineStart = DateTime.UtcNow;

        static void RoutineInvokingTask()
        {
            try
            {
                Random random = new Random((int)(DateTime.UtcNow.Ticks % int.MaxValue));

                Thread.CurrentThread.Join(1000);

                Helper.Print("Initializing", true);
                Routine.Initialise();

                while (true)
                {
                    while (!Routine.Ready) Routine.Wait(1, 2);

                    if (CommittingRoutines.Count == 0)
                    {
                        Helper.Print("Starting new routines", true);

                        foreach (Func<bool> routine in Routines.OrderBy(i => random.Next()).ToArray()) CommittingRoutines.Enqueue(routine);
                    }

                    while (CommittingRoutines.Count > 0)
                    {
                        Func<bool> routine = CommittingRoutines.Peek();

                        if (random.Next(0, 100) < 30 && routine != Routine.GatherResources)
                        {
                            CommittingRoutines.Dequeue();
                            continue;
                        }

                        RoutineStart = DateTime.UtcNow;
                        Helper.Print("Running " + routine.Method.Name, true);
                        routine();
                        CommittingRoutines.Dequeue();
                    }

                    RoutineStart = DateTime.UtcNow;
                    Helper.Print("Running SwitchCharacter", true);
                    Routine.SwitchCharacter();
                    Routine.Wait(10, 15);
                }

            }
            catch (ThreadAbortException)
            {
                Helper.Print("Routines stopped", true);
            }
        }

        static void HangProtectionTask()
        {
            try
            {
                while (true)
                {
                    Process[] processes = Process.GetProcessesByName("MEmu");

                    if ((DateTime.UtcNow - Device.ScreenStamp).TotalSeconds > 5 || (DateTime.UtcNow - RoutineStart).TotalMinutes > 15 || processes.Length == 0)
                    {
                        StopRoutines();

                        Helper.Print("Hang protection activated", true);

                        if (processes.Length > 0)
                        {
                            Helper.Print("Stopping MEmu instances");
                            foreach (Process process in processes)
                            {
                                try
                                {
                                    process.Kill();
                                }
                                catch (Exception)
                                {
                                }
                            }

                            Routine.Wait(10, 15);
                        }

                        Helper.Print("Restarting MEmu");

                        Process.Start(Path.Combine(Helper.MEmuPath, "MEmu.exe"));

                        Helper.Print("Restarting adb connection");

                        DateTime start = DateTime.UtcNow;

                        while ((DateTime.UtcNow - start).TotalMinutes < 5)
                        {
                            Device.Initialise();

                            if (Device.Tap("icon.rok"))
                            {                                
                                Helper.Print("Starting RoK");

                                RoutineStart = DateTime.UtcNow;
                                StartRountines();

                                break;
                            }

                            Routine.Wait(1, 2);
                        }
                    }

                    Thread.CurrentThread.Join(1000);
                }
            }
            catch (ThreadAbortException)
            {
                Helper.Print("Hang protection disabled", true);
            }
        }

        static void VerificationModeTask()
        {
            try
            {
                while (true)
                {
                    if (Device.Match("button.verify", out Rectangle verify))
                    {
                        HangProtectionThread.Abort();
                        StopRoutines();
                        Routine.Wait(1, 2);

                        Helper.Print("Verification mode activated", true);

                        HttpClient client = new HttpClient(new HttpClientHandler { UseProxy = false, Proxy = null });

                        JavaScriptSerializer jss = new JavaScriptSerializer();

                        HttpContent content = new StringContent(jss.Serialize(new
                        {
                            receivers = new string[] { "hoai4285@gmail.com" },
                            name = "RoK Request",
                            content = "<p>Verification mode activated at " + DateTime.Now + "</p><a href=\"http://api.ahacafe.vn/rok.html\">Solve it now</a>",
                            subject = "Verification mode activated",
                            mail_address_name = "info"

                        }), Encoding.UTF8, "application/json");

                        client.PostAsync("http://api.jvjsc.com:6245/mail/send", content).ContinueWith(task =>
                        {
                            client.Dispose();
                            content.Dispose();
                        });

                        Helper.Print("Waiting for user intervention", true);

                        while (Process.GetProcessesByName("MEmu").Length > 0 && !Routine.Ready) Routine.Wait(1, 2);

                        RoutineStart = DateTime.UtcNow;
                        HangProtectionThread = new Thread(new ThreadStart(HangProtectionTask));
                        HangProtectionThread.Start();
                    }

                    Thread.CurrentThread.Join(1000);
                }
            }
            catch (ThreadAbortException)
            {
            }
        }

        static Thread HangProtectionThread = null;
        static Thread RoutineInvokingThread = null;
        static object Locker = new object();

        static void StopRoutines()
        {
            lock (Locker)
            {
                if (RoutineInvokingThread?.IsAlive ?? false)
                {
                    RoutineInvokingThread.Abort();
                    Routine.Wait(1, 2);
                }
            }
        }

        static void StartRountines()
        {
            lock (Locker)
            {
                StopRoutines();

                RoutineInvokingThread = new Thread(new ThreadStart(RoutineInvokingTask));
                RoutineInvokingThread.Start();
            }
        }

        static void MessengerRegister(MessengerClient client)
        {
            client.Register("HVV RoK Bot").ContinueWith(result =>
            {
                if (!result.Result) MessengerRegister(client);
            });
        }

        static void MessengerListener()
        {
            ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First(i => i.FormatID == ImageFormat.Jpeg.Guid);
            EncoderParameters parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 30L);

            using (MessengerClient client = new MessengerClient("api.ahacafe.vn", 100))
            {
                MessengerRegister(client);

                DateTime lastScreenRequest = DateTime.UtcNow;

                Parallel.Start(() =>
                {
                    while (true)
                    {
                        DateTime start = DateTime.UtcNow;

                        if ((DateTime.UtcNow - lastScreenRequest).TotalSeconds < 10)
                        {                            
                            using (Bitmap screen = Device.Screen)
                            {
                                if (screen == null)
                                {
                                    client.Delete("screen").Wait();
                                }
                                else
                                {
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        screen.Save(ms, encoder, parameters);
                                        client.Pusblish("screen", Convert.ToBase64String(ms.ToArray())).Wait();
                                    }
                                }
                            }
                        }

                        Thread.CurrentThread.Join(Math.Max(1, 40 - (int)(DateTime.UtcNow - start).TotalMilliseconds));
                    }
                });

                client.ChannelTerminated += () => MessengerRegister(client);
                
                client.PackageReceived += pkg =>
                {
                    switch (pkg.Type)
                    {
                        case "pull": lastScreenRequest = DateTime.UtcNow; break;

                        case "push":

                            string[] cmds = pkg.Data.Split(' ');

                            if (cmds.Length == 3 && int.TryParse(cmds[1], out int x) && int.TryParse(cmds[2], out int y))
                            {
                                switch (cmds[0])
                                {
                                    case "tap": Device.Tap(x, y); break;
                                    case "press": Device.Press(x, y); break;
                                    case "move": Device.MoveTo(x, y); break;
                                    case "release": Device.Release(); break;
                                }
                            }

                            break;

                        case "kill":

                            try
                            {
                                foreach (Process process in Process.GetProcessesByName("MEmu")) process.Kill();
                            }
                            catch (Exception e)
                            {

                            }

                            break;
                    }
                };

                while (true) Thread.CurrentThread.Join(1000);
            }
        }


        static void Main(string[] args)
        {
            
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.ServicePointManager.UseNagleAlgorithm = false;

            Helper.Print("Starting threads", true);

            Thread V = new Thread(new ThreadStart(VerificationModeTask));
            HangProtectionThread = new Thread(new ThreadStart(HangProtectionTask));
            
            Device.Initialise();

            StartRountines();
            HangProtectionThread.Start();
            V.Start();
            
            Thread M = new Thread(new ThreadStart(MessengerListener));
            M.Start();

            while (true) Thread.CurrentThread.Join(1000);
        }

        #region deprecated

        static void SlideVerificationTask()
        {
            try
            {
                while (true)
                {
                    if (Device.Match("button.verify", out Rectangle verify))
                    {
                        StopRoutines();

                        Helper.Print("Verification solver activated", true);

                        while (Device.Match("button.verify", out verify))
                        {
                            Helper.Print("Accquiring puzzle");

                            Device.Tap(verify);
                            Routine.Wait(5, 6);

                            if (!Device.Match("button.slider", out Rectangle slider))
                            {
                                Helper.Print("Puzzle not found, retry after 1-2 seconds");

                                Device.Tap(10, 10);
                                Routine.Wait(1, 2);

                                continue;
                            }

                            int top = 0x75, left = 0xc9, right = 0x1b5, bottom = 0x105;

                            int height = bottom - top;
                            int width = right - left;

                            using (Bitmap puzzle = Helper.Crop(Device.Screen, new Rectangle { X = left, Y = top, Width = width, Height = height }))
                            {
                                if (Helper.Solve(puzzle, out int offsetX))
                                {
                                    Device.Swipe(slider, offsetX, Helper.RandomGenerator.Next(-5, 6), Helper.RandomGenerator.Next(1000, 1500));

                                    Routine.Wait(3, 5);

                                    if (Device.Match("button.slider", out slider))
                                    {
                                        Helper.Print("False positive, retry after 1-2 seconds");

                                        Device.Tap(10, 10);
                                        Routine.Wait(1, 2);
                                    }
                                    else
                                    {
                                        Helper.Print("Puzzle solved");
                                        StartRountines();
                                    }
                                }
                                else
                                {
                                    Helper.Print("Solution not found, retry after 1-2 seconds");
                                    Device.Tap(10, 10);
                                    Routine.Wait(1, 2);
                                }
                            }
                        }
                    }

                    Thread.CurrentThread.Join(1000);
                }
            }
            catch (ThreadAbortException)
            {
            }
        }

        #endregion
    }
}
