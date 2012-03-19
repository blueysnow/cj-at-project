using System;
using System.Collections.Generic;
using TradeLink.Common;
using TradeLink.API;
using System.ComponentModel;
using TicTacTec.TA.Library; // to use TA-lib indicators

namespace Responses
{
    public enum tradeType { Init, Flat, GoLong, GoShort, SkipLong, SkipShort };
    public enum SystemType { System_One=1, System_Two };
    public enum UnitSizing { Automatic_Sizing, Manual_Sizing };
    

    public class tradeLog
    {
        public tradeType action;
        public decimal entry, exit, stop; 
        public int unit;
    };
    //        decimal accountSize = 100000m;   // To be updated from account query
    //        decimal accountRiskPercent = 0.01m;
    //        int entryLength = 20;   int exitLength = 10;    int failSafeEntryLength = 55;
    //        int ATRLength = 20; decimal ATRMultiplier = 2.0m; decimal ATR = 0.0m;
    public class tradeStats
    {
        public decimal accountSize, accountRiskPercent, tradeProfit;
    };

    public class turtleParams
    {
        public SystemType TurtleSystem;
        public int entryLen, exitLen, failSafeEntryLen;
        public int ATRLen; 
        public decimal ATRMultiplier;
        public bool skipConsecutiveTrades;
        public decimal slippagePerRoundTripDollars;
    };
    //decimal longEntry = 0;  decimal shortEntry = 9999999999999999999999999999m;
    //decimal failSafeLongEntry = 0;  decimal failSafeShortEntry = 9999999999999999999999999999m;
    //decimal shortExit = 0;  decimal longExit = 9999999999999999999999999999m;
    //decimal longStop = 0;   decimal shortStop = 0;
    //decimal tradeProfitPoints = -1.0m;
    public class turtleSignal
    {
        public decimal ATR;
        public decimal longEntry = 0, shortEntry = 9999999999999999999999999999m, failSafeLongEntry = 0, failSafeShortEntry = 9999999999999999999999999999m;
        public decimal longExit = 9999999999999999999999999999m, shortExit = 0;
        public decimal longStop = 0, shortStop = 0;
    };

    public class TurtleResponse : ResponseTemplate
    {
        // parameters of this system
        [Description("Total Profit Target")]
        public decimal TotalProfitTarget { get { return _totalprofit; } set { _totalprofit = value; } }
        [Description("Entry size when signal is found")]
        public int EntrySize { get { return _entrysize; } set { _entrysize = value; } }
        [Description("Default bar interval for this response.")]
        public BarInterval Interval { get { return _barinterval; } set { _barinterval = value; } }
        [Description("bars back when calculating sma")]
        public int BarsBack { get { return _barsback; } set { _barsback = value; } }
        [Description("shutdown time")]
        public int Shutdown { get { return _shutdowntime; } set { _shutdowntime = value; } }

        /*
        #hint: Implementation of the Turtle trading System <b> You should be thoroughly familiar with the Turtle Rules before using this system </b>
        #hint TurtleSystem: Chose which Turtle System to use
        #hint statusPanel: Select what legend appears at the top
        #hint accountSize: Set account balance used for position sizing
        #hint accountRiskPercent: Set percentage of account risked per trade
        #hint entryLength: Length of primary breakout channel
        #hint exitLength: Length of exit channel
        #hint failSafeEntryLength: Length of failsafe breakout channel for System One or normal breakout channel for System 2
        #hint ATRLength: Period used for ATR calculation
        #hint ATRMultiplier: ATR multiplier used for volatility stop
        #hint manualTradeUnit: Manual entry for number of trade units to use (Used to override the automated trade sizing algorithm)
        #hint manualTickValue: Value for each tick of the traded symbol (Used when the automatic system fails)
        #hint unitSizing: Chose automatic trade unit sizing or manual
        #hint tickValue: Chose automated symbol tick value calculation or manual (Used if automatic system fails)
        #hint skipConsecutiveTrades: Choose whether to skip alternating trades after a successful trade (System One only)
        #hint showBubbles: Display or hide Turtle price bubbles
        #hint slippagePerRoundTripDollars: Amount deducted from each round trip trade profit to account for slippage and commissions
         */
//        const int System_One = 1; const int System_Two = 2; const int Automatic_Sizing = 0; const int Manual_Sizing = 1; const int Automatic_Tick_Value = 0; const int Manual_Tick_Value = 1;

        public int manualTradeUnit = 1;
        public const decimal manualTickValue = 0.25m;
        public UnitSizing unitSizing = UnitSizing.Manual_Sizing;
        public decimal tickValue = manualTickValue;
        public bool skipConsecutiveTrades = false;
        decimal slippagePerRoundTripDollars = 0.0m;


        public tradeStats myStats;
        public turtleParams myParams;
        public List<tradeLog> myTrades = new List<tradeLog>();
        public List<turtleSignals> mySignals = new List<turtleSignals>();

        bool _black = false;
        // this function is called the constructor, because it sets up the response
        // it is run before all the other functions, and has same name as my response.
        public TurtleResponse() : this(true) 
        {

            //int lastTrade = -1;
            // account size to be updated from account query
            myStats.accountSize = 1000000m; myStats.accountRiskPercent = 0.01m; myStats.tradeProfit = 0m;
            myParams.ATRLen = 20; myParams.ATRMultiplier = 2.0m;
            myParams.entryLen = 20; myParams.exitLen = 10; myParams.failSafeEntryLen = 55;
            myParams.skipConsecutiveTrades = false; myParams.TurtleSystem = SystemType.System_One;
            myParams.slippagePerRoundTripDollars = 0m;
            tradeLog initTrade = new tradeLog();
            initTrade.action = tradeType.Init;
            myTrades.Add(initTrade);
        }
        public TurtleResponse(bool prompt)
        {
            _black = !prompt;
            // handle when new symbols are added to the active tracker
            _active.NewTxt += new TextIdxDelegate(_active_NewTxt);

            // set our indicator names, in case we import indicators into R
            // or excel, or we want to view them in gauntlet or kadina
            Indicators = new string[] { "Time", "Turtle" };
        }
/* right now doing this might just confuse the program
        public override void Reset()
        {
            // enable prompting of system parameters to user,
            // so they do not have to recompile to change things
            ParamPrompt.Popup(this, true, _black);

            // only build bars for user's interval
            blt = new BarListTracker(Interval);

            // only calculate on new bars
            blt.GotNewBar += new SymBarIntervalDelegate(blt_GotNewBar);
        }
        */

        // wait for fill
        GenericTracker<bool> _wait = new GenericTracker<bool>();
        // track whether shutdown 
        GenericTracker<bool> _active = new GenericTracker<bool>();
        // hold last ma
        GenericTracker<decimal> _sma = new GenericTracker<decimal>();

        void _active_NewTxt(string txt, int idx)
        {
            // go ahead and notify any other trackers about this symbol
            _wait.addindex(txt, false);
            _sma.addindex(txt, 0);
        }
        
        void blt_GotNewBar(string symbol, int interval)
        {
            int idx = _active.getindex(symbol);
            BarList myBarList = blt[symbol];
            Bar curBar = myBarList.RecentBar;
            int barCount = myBarList.Count;   int lastBar = barCount - 1;
            decimal oneTick = tickValue;    //this is hard-coded for ES contracts
            tradeLog curTrade = new tradeLog();
            turtleSignal curSignal = new turtleSignal();

            //doing manual sizing here
            int units = manualTradeUnit;
            if (myBarList.Count < myParams.ATRLen)
            {
                D("Error: waiting for more bars. or you can request more history data"); return;
            }

            // calculate the SMA using closing prices for so many bars back
            //decimal SMA = MyCalc.Avg(Calc.EndSlice(blt[symbol].Close(), _barsback));
            //TODO: optimize this one calculation
            curSignal.ATR = MyCalc.AverageTrueRange(blt[symbol], myParams.ATRLen);
            if (curSignal.ATR < 0) { D("Error calc ATR"); return; }
    
            switch(myParams.TurtleSystem){
    
                case SystemType.System_One:

                    if(myTrades.Count == 0) //essentially trade.Init
                    {
                        lastTrade = 0;  
                        entry = 0;  exit = 0;   
                        curTrade = trade.Flat;
                        myTrades.Add(curTrade);
                        tradeProfitPoints = 0;
                        stop = 0;
                        entryUnits = units; //using this for now. 
                    }
                    break;
    
                    switch(myTrades[myTrades.Count-1])
                    {
                        case trade.Init:
                            {
                                lastTrade = 0;


                            }
                        case trade.Flat:
            
                            if ((curBar.High > longEntry && lastTrade == 1 && curBar.High < failSafeLongEntry)  && skipConsecutiveTrades)
                            {
                                entry = Math.Max(longEntry+oneTick, curBar.Low);
                                exit = 0;
                                curTrade = trade.SkipLong;
                                tradeProfitPoints = 0;
                                stop = entry - ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;
                            }                
                            else if (( (curBar.Low < shortEntry && lastTrade == 1 && curBar.Low > failSafeShortEntry)) && skipConsecutiveTrades)
                            {
                                entry = Math.Min(shortEntry-oneTick, curBar.High);
                                exit = 0;
                                curTrade = trade.SkipShort;
                                tradeProfitPoints = 0;
                                stop = entry+ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;
                            }        
                            else if (curBar.High > longEntry)
                            {
                                entry = Math.Max(longEntry+oneTick, curBar.Low);
                                exit = 0;
                                curTrade = trade.GoLong;
                                tradeProfitPoints = 0;
                                stop = entry-ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;                   
                            }                   
                            else if (curBar.Low < shortEntry) 
                            {
                                entry = Math.Min(shortEntry-oneTick, curBar.High);
                                exit = 0;
                                curTrade = trade.GoShort;
                                tradeProfitPoints = 0;
                                stop = entry+ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;
                            }             
                            else 
                            {
                                entry = 0;
                                exit = 0;
                                curTrade = trade.Flat;
                                tradeProfitPoints = 0;
                                stop = -1.0m;
                                entryUnits = entryUnits[1];
                                lastTrade = lastTrade[1]; 
                            }
        
                        case trade.GoLong:
                            entryUnits = entryUnits[1];
                            if (curBar.Low < longExit || curBar.Low < stop[1])
                            {
                                exit = Math.Min(curBar.Open, Math.Max(longExit-oneTick, stop[1]-oneTick));
                                entry = entry[1];
                                curTrade = trade.Flat;
                                tradeProfitPoints = exit - entry[1];
                                stop = -1m;
                                lastTrade = tradeProfitPoints > 0 ? 1 : 0;               
                            } 
                            else
                            {
                                exit = 0;
                                entry = entry[1];
                                curTrade = trade.GoLong;
                                tradeProfitPoints = 0;
                                stop = stop[1];
                                lastTrade = lastTrade[1];
                            }        
                        case trade.GoShort:
                            entryUnits = entryUnits[1];
                            if (curBar.High > shortExit || curBar.High > stop[1])
                            {
                                exit = Math.Max(curBar.Open, Math.Min(shortExit+oneTick, stop[1]+oneTick));
                                entry = entry[1];
                                curTrade = trade.Flat;
                                tradeProfitPoints = entry[1]-exit;
                                stop = -1m;
                                lastTrade = tradeProfitPoints > ? 1 : 0;
                            } 
                            else
                            {
                                exit = 0;
                                entry = entry[1];
                                curTrade = trade.GoShort;
                                tradeProfitPoints = 0;
                                stop = stop[1];
                                lastTrade = lastTrade[1];
                            }        
                        case trade.SkipLong:
                            if (curBar.Low < longExit || curBar.Low < stop[1])
                            {
                                exit = Math.Min(curBar.Open, Math.Max(longExit-oneTick, stop[1]-oneTick));
                                entry = entry[1];
                                curTrade = trade.Flat;
                                tradeProfitPoints = exit - entry[1];
                                stop = -1m;
                                lastTrade = 0;
                                entryUnits = 0;
                            } 
                            else if (curBar.High > failSafeLongEntry)
                            {
                                entry = Math.Max(failSafeLongEntry+oneTick, curBar.Low);
                                exit = 0;
                                curTrade = trade.GoLong;
                                tradeProfitPoints = 0;
                                stop = Math.Max(failSafeLongEntry+oneTick, curBar.Low)-ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;
                            }
                            else
                            {
                                exit = 0;
                                entry = entry[1];
                                curTrade = trade.SkipLong;
                                tradeProfitPoints = 0;
                                stop = stop[1];
                                lastTrade = lastTrade[1];
                                entryUnits = 0;
                            }
        
                        case trade.SkipShort:
                            if (curBar.High > shortExit || curBar.High > stop[1])
                            {
                                entryUnits = 0;
                                exit = Math.Max(curBar.Open, Math.Min(shortExit+oneTick, stop[1]+oneTick));
                                entry = entry[1];
                                curTrade = trade.Flat;
                                tradeProfitPoints = entry[1]-exit;
                                stop = -1m;
                                lastTrade = 0;
                            } 
                            else if (curBar.Low < failSafeShortEntry)
                            {   
                                entry = Math.Min(failSafeShortEntry-oneTick, curBar.High);
                                exit = 0;
                                curTrade = trade.GoShort;
                                tradeProfitPoints = 0;
                                stop = entry+ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;
                            }
                            else
                            {
                                exit = 0;
                                entry = entry[1];
                                curTrade = trade.SkipShort;
                                tradeProfitPoints = 0;
                                stop = stop[1];
                                lastTrade = lastTrade[1];
                                entryUnits = 0;
                            }
                        }
                        break;
                case SystemType.System_Two:
    
                    switch(trade[1]){
    
                        case trade.Init:
                            lastTrade = 0;
                            entry = 0;
                            exit = 0;
                            curTrade = barNumber() >=1 ? trade.Flat : trade.Init;
                            tradeProfitPoints = 0;
                            stop = 0;
                            entryUnits = unit;
                        case trade.Flat:
                            if( curBar.High > failSafeLongEntry )
                            {
                                entry = Math.Max(failSafeLongEntry+oneTick, curBar.Low);
                                exit = 0;
                                curTrade = trade.GoLong;
                                tradeProfitPoints = 0;
                                stop = entry-ATRMultiplier*ATR;
                                entryUnits = units;
                                lastTrade = 0;
                            }
                            else if ( curBar.Low < failSafeShortEntry )
                            {
                                entry = Math.Min(failSafeShortEntry-oneTick, curBar.High);
                                exit = 0;
                                curTrade = trade.GoShort;
                                tradeProfitPoints = 0;
                                stop = entry+ATRMultiplier*ATR;
                                entryUnits = unit;
                                lastTrade = 0;
                            } 
                            else 
                            {
                                entry = 0;
                                exit = 0;
                                curTrade = trade.Flat;
                                tradeProfitPoints = 0;
                                stop = -1m;
                                entryUnits = entryUnits[1];
                                lastTrade = 0;
                            }
    
                        case trade.GoLong:
                            entryUnits = entryUnits[1];
                            if (curBar.Low < longExit || curBar.Low < stop[1])
                            {
                                exit = Math.Min(curBar.Open, Math.Max(longExit-oneTick, stop[1]-oneTick));
                                entry = entry[1];
                                curTrade = trade.Flat;
                                tradeProfitPoints = exit - entry[1];
                                stop = -1m;
                                lastTrade = 0;
                            } 
                            else
                            {
                                exit = 0;
                                entry = entry[1];
                                curTrade = trade.GoLong;
                                tradeProfitPoints = 0;
                                stop = stop[1];
                                lastTrade = 0;
                            }
    
                        case trade.GoShort: 
                            entryUnits = entryUnits[1];
                            if (curBar.High > shortExit || curBar.High > stop[1] )
                            {
                                exit = Math.Max(curBar.Open, Math.Min(shortExit+oneTick, stop[1]+oneTick));
                                entry = entry[1];
                                curTrade = trade.Flat;
                                tradeProfitPoints = entry[1]-exit;
                                stop = -1m;
                                lastTrade = 0;
                            } 
                            else
                            {
                                exit = 0;
                                entry = entry[1];
                                curTrade = trade.GoShort;
                                tradeProfitPoints = 0;
                                stop = stop[1];
                                lastTrade = 0;
                            }
                
                        case trade.SkipShort:
                            entryUnits = 0;
                            exit = 0;
                            entry = 0;
                            curTrade = trade.GoShort;
                            tradeProfitPoints = 0;
                            stop = 0;
                            lastTrade = 0;
    
                        case trade.SkipLong:
                            entryUnits = 0;
                            exit = 0;
                            entry = 0;
                            trade = trade.GoShort;
                            tradeProfitPoints = 0;
                            stop = 0;
                            lastTrade = 0;
                    }
                    break;
}

            //
 //           D("received new bar. close is "+ (blt[symbol].RecentBar.Close).ToString() +"SMA is "+SMA.ToString());


/*  for the moment let me assume we always get a fill
            //ensure we aren't waiting for previous order to fill
            if (!_wait[symbol])
            {

                // if we're flat and not waiting
                if (pt[symbol].isFlat)
                {
                    // if our current price is above SMA, buy
                    if (blt[symbol].RecentBar.Close > ATR)
                    {
                        D("crosses above MA, buy");
                        sendorder(new BuyMarket(symbol, EntrySize));
                        // wait for fill
                        _wait[symbol] = true;
                    }
                    // otherwise if it's less than SMA, sell
                    if (blt[symbol].RecentBar.Close < ATR)
                    {
                        D("crosses below MA, sell");
                        sendorder(new SellMarket(symbol, EntrySize));
                        // wait for fill
                        _wait[symbol] = true;
                    }
                }
                else if ((pt[symbol].isLong && (blt[symbol].RecentBar.Close < SMA))
                    || (pt[symbol].isShort && (blt[symbol].RecentBar.Close > SMA)))
                {
                    D("counter trend, exit.");
                    sendorder(new MarketOrderFlat(pt[symbol]));
                    // wait for fill
                    _wait[symbol] = true;
                }
            }
            else
            {
                D("no action, waiting for previous order to fill");
            }
*/


            // this way we can debug our indicators during development
            // indicators are sent in the same order as they are named above
            sendindicators(new string[] { time.ToString(), SMA.ToString("N2") });

            // draw the MA as a line
            sendchartlabel(SMA, time);
        }

        // turn on bar tracking
        BarListTracker blt = new BarListTracker();
        // turn on position tracking
        PositionTracker pt = new PositionTracker();

        // keep track of time for use in other functions
        int time = 0;

        // got tick is called whenever this strategy receives a tick
        public override void GotTick(Tick tick)
        {
            // keep track of time from tick
            time = tick.time;
            // ensure response is active
            if (!isValid) return;
            // ensure we are tracking active status for this symbol
            int idx = _active.addindex(tick.symbol, true);
            // if we're not active, quit
            if (!_active[idx]) return;
            // check for shutdown time
            if (tick.time > Shutdown)
            {
                // if so shutdown
                shutdown();
                // and quit
                return;
            }
            // apply bar tracking to all ticks that enter
            blt.newTick(tick);

            // ignore anything that is not a trade
            if (!tick.isTrade) return;

            // if we made it here, we have enough bars and we have a trade.

            // exits are processed first, lets see if we have our total profit
            if (Calc.OpenPL(tick.trade, pt[tick.symbol]) + pt[tick.symbol].ClosedPL > TotalProfitTarget)
            {
                // if we hit our target, shutdown trading on this symbol
                shutdown(tick.symbol);
                // don't process anything else after this (entries, etc)
                return;
            }

        }

        void shutdown()
        {
            D("shutting down everything");
            foreach (Position p in pt)
                sendorder(new MarketOrderFlat(p));
            isValid = false;
        }

        void shutdown(string sym)
        {
            // notify
            D("shutting down " + sym);
            // send flat order
            sendorder(new MarketOrderFlat(pt[sym]));
            // set inactive
            _active[sym] = false;
        }

        public override void GotFill(Trade fill)
        {
            // make sure every fill is tracked against a position
            pt.Adjust(fill);
            // get index for this symbol
            int idx = _wait.getindex(fill.symbol);
            // ignore unknown symbols
            if (idx < 0) return;
            // stop waiting
            _wait[fill.symbol] = false;
            // chart fills
            sendchartlabel(fill.xprice, time, TradeImpl.ToChartLabel(fill), fill.side ? System.Drawing.Color.Green : System.Drawing.Color.Red);
        }

        public override void GotPosition(Position p)
        {
            // make sure every position set at strategy startup is tracked
            pt.Adjust(p);
        }

        // these variables "hold" the parameters set by the user above
        // also they are the defaults that show up first
        int _barsback = 9;
        BarInterval _barinterval = BarInterval.FiveMin;
        int _entrysize = 1;
        decimal _totalprofit = 200;
        int _shutdowntime = 235900;
    }

    /// <summary>
    /// this is the same as TurtleResponse, except it runs without prompting user
    /// </summary>
    public class TurtleResponseAuto : TurtleResponse
    {
        public TurtleResponseAuto() : base(false) { }
    }



#warning If you get errors about missing references to TradeLink.Common or TradeLink.Api, choose Project->Add Reference->Browse to folder where you installed tradelink (usually Program Files) and add a reference for each dll.
}
