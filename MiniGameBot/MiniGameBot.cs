using System.Diagnostics;
using System.Threading;
using System.Windows.Media;
using System.Xml;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Interfaces;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Pathing.Service_Navigation;
using ff14bot.Objects;
using Clio.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using ff14bot.RemoteWindows;
using TreeSharp;

using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.RemoteWindows;
using System.ComponentModel;
using System.Threading;

using Action = TreeSharp.Action;
using ff14bot.AClasses;

namespace MiniGameBot
{
    public enum PlayStrategy
    {
        Simple, // one time play
        UfoCatcher, // The Moogle's Paw
        FiveTime, // play time int a row
    }

    public class MiniGameBot : BotBase
    {
        private MgpStatistic statistic = new MgpStatistic();
        private Composite _root;

        private MiniGameForm _settings;
        public static MiniGameSettings Settings = MiniGameSettings.Instance;


        private Dictionary<string, MiniGameConfig> availableConfigs = new Dictionary<string, MiniGameConfig>
        {
            {
                "Cuff-a-Cur",
                new MiniGameConfig(
                    "PunchingMachine",
                    PlayStrategy.Simple,
                    new List<Npc> {
                        new Npc(2005029, 144, new Vector3(24.82569f, -5.000007f, -50.57803f))
                        , new Npc(2005029, 144, new Vector3(13.04904f, -5.000005f, -52.66679f))
                    }
                )
            },
            {
                "Crystal Tower Striker",
                new MiniGameConfig(
                    "Hummer",
                    PlayStrategy.Simple,
                    new List<Npc> {
                        new Npc(2005035, 144, new Vector3(25.18871f, 4.91873f, 92.24194f))
                        , new Npc(2005035, 144, new Vector3(24.33813f, 4.91873f, 102.5402f))
                    }
                )
            },
            {
                "The Moogle's Paw",
                new MiniGameConfig(
                    "UfoCatcher",
                    PlayStrategy.Simple,
                    new List<Npc> {
                        new Npc(2005036, 144, new Vector3(112.2101f, -5.00001f, -57.23045f))
                        , new Npc(2005036, 144, new Vector3(112.2101f, -5.00001f, -57.23045f))
                    }
                )
            },
            {
                "Monster Toss",
                new MiniGameConfig(
                    "BasketBall",
                    PlayStrategy.FiveTime,
                    new List<Npc> {
                        new Npc(2004804, 144, new Vector3(35.42477f, 5.193397f, 17.80501f))
                        , new Npc(2004804, 144, new Vector3(43.49005f, 5.194275f, 17.04697f))
                    }
                )
            }

        };

        public string SelectedGame
        {
            get
            {
                return Settings.SelectedGame;
            }
        }

        public int PlayCount
        {
            get
            {
                return Settings.Count;
            }
        }

        private Npc TargetNpc;
        private PlayStrategy Strategy;
        private GameWindowBase gameWindow;
        private int playCount;

        public MiniGameBot()
        {

        }

        public GameObject NPC
        {
            get
            {
                return GameObjectManager.GetObjectsByNPCId(TargetNpc.NpcId)
                    .OrderBy(p => p.Distance(Core.Me.Location))
                    .FirstOrDefault(n => n.IsVisible && n.IsTargetable);
            }

        }
        public override string Name
        {
            get { return "MiniGameBot"; }
        }

        public override bool IsAutonomous
        {
            get { return true; }
        }

        public static Version Version = new Version(1, 0, 0);

        public string LoggerPrefix
        {
            get
            {
                return string.Format("[{0} v{1}] ", Name, Version);
            }
        }

        public void Log(string format, params object[] args)
        {
            Logging.Write(Colors.SandyBrown, LoggerPrefix + string.Format(format, args));
        }

        public void Log(string message)
        {
            Logging.Write(Colors.SandyBrown, LoggerPrefix + message);
        }

        public override void OnButtonPress()
        {
            if (_settings == null || _settings.IsDisposed)
                _settings = new MiniGameForm();
            try
            {
                _settings.Show();
                _settings.Activate();
            }
            catch (ArgumentOutOfRangeException ee)
            {
            }
        }

        public override void Start()
        {
            FinalizedPath = null;
            findPathRequested = false;
            playCount = 0;
            statistic.Reset();

            if (!availableConfigs.ContainsKey(SelectedGame))
            {
                TreeRoot.Stop();
                return;
            }

            var config = availableConfigs[SelectedGame];
            var npcList = config.Npcs;

            Strategy = config.Strategy;

            if (npcList == null || npcList.Count == 0)
            {
                TreeRoot.Stop();
                return;
            }

            Random r = new Random();
            int npcIndexR = r.Next(0, 1000);
            int npcIndex = npcIndexR % npcList.Count;

            TargetNpc = npcList[npcIndex];

            gameWindow = new SimpleGameWindow(config.WindowName);

            Navigator.NavigationProvider = new ServiceNavigationProvider();
            Navigator.PlayerMover = new SlideMover();

            Log("Starting.");
        }

        public override void Stop()
        {
            IDisposable disposable = Navigator.NavigationProvider as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
            Navigator.NavigationProvider = null;

            Log(statistic.GetStatistic());
            Log("Stopping.");
        }

        public override PulseFlags PulseFlags
        {
            get { return PulseFlags.All; }
        }

        public override bool RequiresProfile
        {
            get { return false; }
        }

        public override bool WantButton
        {
            get { return true; }
        }

        bool findPathRequested = false;
        public Queue<NavGraph.INode> FinalizedPath;

        private async Task<bool> InitPathToNpc()
        {
            findPathRequested = true;
            Queue<NavGraph.INode> res = await NavGraph.GetPathAsync(TargetNpc.ZoneId, TargetNpc.Location);
            this.Log("GetPathAsync finished. Result is {0}empty", res == null ? "" : "not ");

            if (res == null)
            {
                return false;
            }

            FinalizedPath = res;
            
            return true;
        }

        private async Task<bool> InitPathToNpcWaiting()
        {
            Log("Waiting for path generation");
            await Coroutine.Sleep(1000);
            return true;
        }

        private async Task<bool> PlayRoutine()
        {
            if (PlayCount > 0)
            {
                playCount += 1;
                Log("Play {0} of {1}.", playCount, PlayCount);
            }

            if (Strategy == PlayStrategy.Simple)
            {
                await Coroutine.Sleep(500);
                SimpleGameWindow swnd = gameWindow as SimpleGameWindow;
                if (swnd == null)
                {
                    Log("Error - Simplegamewindow is null, {0}", gameWindow == null ? "empty" : gameWindow.Name);
                    return false;
                }

                Random r = new Random();
                int c = r.Next(0, 10000);
                ulong score = (ulong)(1980 + (c % 20));

                swnd.Play(3, score);

                await Coroutine.Wait(50000, () => GoldSaucerRewardWindow.IsOpen);
                return true;
            }

            if (Strategy == PlayStrategy.UfoCatcher)
            {
                await Coroutine.Sleep(1500);
                SimpleGameWindow swnd = gameWindow as SimpleGameWindow;
                if (swnd == null)
                {
                    Log("Error - Simplegamewindow is null, {0}", gameWindow == null ? "empty" : gameWindow.Name);
                    return false;
                }

                swnd.Play(3, 0);

                await Coroutine.Wait(50000, () => GoldSaucerRewardWindow.IsOpen);
                return true;
            }

            if (Strategy == PlayStrategy.FiveTime)
            {
                await Coroutine.Sleep(500);

                SimpleGameWindow swnd = gameWindow as SimpleGameWindow;
                if (swnd == null)
                {
                    Log("Error - Simplegamewindow is null, {0}", gameWindow == null ? "empty" : gameWindow.Name);
                    return false;
                }

                Random r = new Random();

                int Count = r.Next(5, 7);

                for (int i = 0; i < Count; ++i)
                {
                    swnd.Play(1, 0);
                    await Coroutine.Sleep(3000 + r.Next(0, 1000));
                }

                await Coroutine.Wait(50000, () => GoldSaucerRewardWindow.IsOpen);
                return true;
            }

            return false;
        }

        private async Task<bool> CheckGameLimit()
        {
            if (this.PlayCount <= 0)
                return false;

            if (playCount >= PlayCount)
            {
                TreeRoot.Stop("Game play limit is reached");
                return true;
            }

            return false;
        }

        private bool IsSelectMenuOpen
        {
            get
            {
                return SelectYesno.IsOpen || Talk.DialogOpen || SelectString.IsOpen || SelectIconString.IsOpen;
            }
        }

        public override Composite Root
        {
            get
            {
                return _root ?? (
                    _root = new PrioritySelector(
                        new ActionRunCoroutine(r => CheckGameLimit())
                        , new Decorator(ctx => !this.findPathRequested, new ActionRunCoroutine(r => InitPathToNpc()))
                        , new Decorator(ctx => this.findPathRequested && this.FinalizedPath == null, new ActionRunCoroutine(r => InitPathToNpcWaiting()))

                        , new Decorator(ctx => GoldSaucerRewardWindow.IsOpen, new ActionRunCoroutine(p => GoldSaucerRewardWindow.Close()))

                        , new Decorator(ctx => SelectIconString.IsOpen, new TreeSharp.Action(p => SelectIconString.ClickSlot(0)))
                        , new Decorator(ctx => SelectString.IsOpen, new TreeSharp.Action(p => SelectString.ClickSlot(0)))
                        , new Decorator(ctx => Talk.DialogOpen, new TreeSharp.Action(p => Talk.Next()))
                        , new Decorator(ctx => SelectYesno.IsOpen, new TreeSharp.Action(p => SelectYesno.Yes()))

                        , new Decorator(ctx => !IsSelectMenuOpen && gameWindow.IsOpen, new ActionRunCoroutine(r => PlayRoutine()))

                        , new Decorator(ctx => NPC != null && NPC.IsWithinInteractRange && !MovementManager.IsMoving,
                            new Sequence(
                                new TreeSharp.Action(r => NPC.Interact()),
                                new ActionRunCoroutine(r => Coroutine.Wait(30000, ()=>SelectString.IsOpen)),
                                new TreeSharp.Action(p => SelectString.ClickSlot(0)),
                                new ActionRunCoroutine(r => Coroutine.Wait(30000, () => !SelectString.IsOpen)),
                                new TreeSharp.Action(r => RunStatus.Success)
                            )
                        )
                        , ff14bot.Navigation.NavGraph.NavGraphConsumer(r => FinalizedPath)
                ));
            }
        }
    }

    public class GameWindowBase
    {
        public string Name { get; }
        private AtkAddonControl control;

        public GameWindowBase(string name)
        {
            Name = name;
        }

        public bool IsValid
        {
            get { return Control != null && Control.IsValid; }
        }

        public bool IsOpen
        {
            get { return Control != null; }
        }

        public GameWindowBase Refresh()
        {
            RaptureAtkUnitManager.Update();
            control = RaptureAtkUnitManager.GetWindowByName(Name);
            return this;
        }

        public virtual AtkAddonControl Control
        {
            get { return Refresh().control; }
        }

        public virtual void SendAction(int pairCount, params ulong[] param)
        {
            if (IsValid)
                Control.SendAction(pairCount, param);
        }

        public async Task<bool> Close()
        {
            ushort interval = 250;
            await Coroutine.Sleep(interval / 2);

            try
            {
                SendAction(1, 3, ulong.MaxValue);
            }
            catch (Exception ex)
            {
                return false;
            }

            Refresh();

            if (!IsValid)
                return true;

            await Coroutine.Sleep(interval * 2);
            return true;
        }
    }

    public class SimpleGameWindow : GameWindowBase
    {
        public SimpleGameWindow(string name)
            : base(name)
        {
        }

        public void Play(ulong level, ulong score)
        {
            this.SendAction(3, 3, 0x0B, 3, level, 3, score);
        }
    }

    public class GoldSaucerRewardWindow
    {
        private static GameWindowBase wnd = new GameWindowBase("GoldSaucerReward");

        public static bool IsOpen
        {
            get
            {
                return wnd.IsOpen;
            }
        }

        public static async Task<bool> Close()
        {
            return await wnd.Close();
        }
    }

    public class MgpStatistic
    {
        private DateTime startDate;
        int startMgp;

        public MgpStatistic()
        {

        }

        public void Reset()
        {
            startMgp = CurrentMgp;
            startDate = DateTime.Now;
        }

        public TimeSpan Duration
        {
            get
            {
                return DateTime.Now - startDate;
            }
        }

        public int Count
        {
            get
            {
                return CurrentMgp - startMgp;
            }
        }

        private int CurrentMgp
        {
            get
            {
                return ff14bot.NeoProfiles.ConditionParser.ItemCount(29);
            }
        }

        public string GetStatistic()
        {
            int s = startMgp;
            int e = CurrentMgp;
            int diff = e - s;
            TimeSpan duration = Duration;
            double durationInMin = duration.TotalMinutes;
            string simpleStat = string.Format("Start={0}\tEnd={1}\tIncome={2}", s, e, diff);
            if (durationInMin < 5)
            {
                return simpleStat;
            }

            double mgpForMin = (double)diff / duration.TotalMinutes;
            return string.Format("{0}\tMGP per min={1}", simpleStat, mgpForMin.ToString("F4"));
        }
    }

    public class Npc
    {
        public Npc()
        {

        }

        public Npc(uint npcId, uint zoneId, Vector3 loc)
        {
            NpcId = npcId;
            ZoneId = zoneId;
            Location = loc;
        }

        public uint NpcId { get; set; }
        public Vector3 Location { get; set; }
        public uint ZoneId { get; set; }
    }

    public class MiniGameConfig
    {
        public List<Npc> Npcs { get; }
        public PlayStrategy Strategy { get; }
        public string WindowName { get; }

        public MiniGameConfig()
        {
            Npcs = new List<Npc>();
            Strategy = PlayStrategy.Simple;
        }

        public MiniGameConfig(string windowName, PlayStrategy strategy, List<Npc> npcs)
        {
            WindowName = windowName;
            Npcs = npcs;
            Strategy = strategy;
        }
    }
}