using System;
using System.Collections.Generic;
using TradeLink.Common;
using TradeLink.API;
using System.ComponentModel;
using TicTacTec.TA.Library; // to use TA-lib indicators

namespace Responses
{
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
        const int System_One = 1; const int System_Two = 2; const int Automatic_Sizing = 0; const int Manual_Sizing = 1; const int Automatic_Tick_Value = 0; const int Manual_Tick_Value = 1;
        int TurtleSystem = System_One;
        //int statusPanel = {default CurrentStats, PositionSizing, Historical};

        int manualTradeUnit = 1;
        double manualTickValue = 5.0;
        int unitSizing = Manual_Sizing;
        int tickValue = Automatic_Tick_Value;
        bool skipConsecutiveTrades = false;
        bool showBubbles = true;
        double slippagePerRoundTripDollars = 0.0;

        

        bool _black = false;
        // this function is called the constructor, because it sets up the response
        // it is run before all the other functions, and has same name as my response.
        public TurtleResponse() : this(true) { }
        public TurtleResponse(bool prompt)
        {
            _black = !prompt;
            // handle when new symbols are added to the active tracker
            _active.NewTxt += new TextIdxDelegate(_active_NewTxt);

            // set our indicator names, in case we import indicators into R
            // or excel, or we want to view them in gauntlet or kadina
            Indicators = new string[] { "Time", "Turtle" };
        }
/*
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

        int accountSize = 100000;   // To be updated from account query
        double accountRiskPercent = 1.0;
        int entryLength = 20;   int exitLength = 10;    int failSafeEntryLength = 55;
        int ATRLength = 20; double ATRMultiplier = 2.0; decimal ATR = 0;
        decimal longEntry = 0;  decimal shortEntry = 9999999999999999999999999999m;
        decimal failSafeLongEntry = 0;  decimal failSafeShortEntry = 9999999999999999999999999999m;
        decimal shortExit = 0;  decimal longExit = 9999999999999999999999999999m;
        decimal longStop = 0;   decimal shortStop = 0;
        int trade = 0; decimal entry = -1; decimal exit = -1; decimal tradeProfitPoints = -1; decimal stop = -1; decimal entryUnits = -1; decimal lastTrade = -1;

        void blt_GotNewBar(string symbol, int interval)
        {
            // lets do our entries.  

            int idx = _active.getindex(symbol);

            if (blt[symbol].Count < ATRLength)
            {
                D("Error: waiting for more bars. or you can request history data"); return;
            }

            // calculate the SMA using closing prices for so many bars back
            //decimal SMA = MyCalc.Avg(Calc.EndSlice(blt[symbol].Close(), _barsback));
            ATR = MyCalc.AverageTrueRange(blt[symbol], ATRLength);
            if (ATR < 0) { D("Error calc ATR"); return; }
    
            switch(TurtleSystem){
    
                case System_One:
    
                    switch(trade[1]){
            
            case init:
                
                lastTrade = 0;
                entry = 0;
                exit = 0;
                trade = if barNumber() >=1 then trade.flat else trade.init;
                tradeProfitPoints = 0;
                stop = 0;
                entryUnits = unit;
                        
            case flat:
            
                if ((high > longEntry and lastTrade[1] == 1 and high < failSafeLongEntry)  and skipConsecutiveTrades) then
                {
                    entry = max(longEntry+oneTick, low);
                    exit = 0;
                    trade = trade.skipLong;
                    tradeProfitPoints = 0;
                    stop = entry - ATRMultiplier*atr;
                    entryUnits = 0;
                    lastTrade = 0;
                }
                
                else if (( (low < shortEntry and lastTrade[1] == 1 and low > failSafeShortEntry)) and skipConsecutiveTrades) then
                {
                    entry = min(shortEntry-oneTick, high);
                    exit = 0;
                    trade = trade.skipShort;
                    tradeProfitPoints = 0;
                    stop = entry+ATRMultiplier*atr;
                    entryUnits = 0;
                    lastTrade = 0;
                }
        
                   else if high > longEntry then 
                {
                    entry = max(longEntry+oneTick, low);
                    exit = 0;
                    trade = trade.long;
                    tradeProfitPoints = 0;
                    stop = entry-ATRMultiplier*atr;
                    entryUnits = unit;
                    lastTrade = 0;
                   
                }    
               
                else if low < shortEntry then 
                {
                    entry = min(shortEntry-oneTick, high);
                    exit = 0;
                    trade = trade.short;
                    tradeProfitPoints = 0;
                    stop = entry+ATRMultiplier*atr;
                    entryUnits = unit;
                    lastTrade = 0;
                } 
            
                else 
                {
                    entry = 0;
                    exit = 0;
                    trade = trade.flat;
                    tradeProfitPoints = 0;
                    stop = double.nan;
                    entryUnits = entryUnits[1];
                    lastTrade = lastTrade[1]; 
                }
        
            case long:
                entryUnits = entryUnits[1];
                if low < longExit or low < stop[1] then
                {
                    exit = min(open, max(longExit-oneTick, stop[1]-oneTick));
                    entry = entry[1];
                    trade = trade.flat;
                    tradeProfitPoints = exit - entry[1];
                    stop = double.nan;
                    lastTrade = if tradeProfitPoints > 0 then 1 else 0;               
                    } 
                else
                {
                    exit = 0;
                    entry = entry[1];
                    trade = trade.long;
                    tradeProfitPoints = 0;
                    stop = stop[1];
                    lastTrade = lastTrade[1];
                }
        
            case short:
                 entryUnits = entryUnits[1];
                 if high > shortExit or high > stop[1] then
                {
                    exit = max(open, min(shortExit+oneTick, stop[1]+oneTick));
                    entry = entry[1];
                    trade = trade.flat;
                    tradeProfitPoints = entry[1]-exit;
                    stop = double.nan;
                    lastTrade = if tradeProfitPoints > 0 then 1 else 0;
                } 
                else
                {
                    exit = 0;
                    entry = entry[1];
                    trade = trade.short;
                    tradeProfitPoints = 0;
                    stop = stop[1];
                    lastTrade = lastTrade[1];
                }
        
            case skipLong:
                if low < longExit or low < stop[1] then
                {
                    exit = min(open, max(longExit-oneTick, stop[1]-oneTick));
                    entry = entry[1];
                    trade = trade.flat;
                    tradeProfitPoints = exit - entry[1];
                    stop = double.nan;
                    lastTrade = 0;
                    entryUnits = 0;
                } 
                else if high > failSafeLongEntry then
                {
                    entry = max(failSafeLongEntry+oneTick, low);
                    exit = 0;
                    trade = trade.long;
                    tradeProfitPoints = 0;
                    stop = max(failSafeLongEntry+oneTick, low)-ATRMultiplier*atr;
                    entryUnits = unit;
                    lastTrade = 0;
                }
                else
                {
                    exit = 0;
                    entry = entry[1];
                    trade = trade.SkipLong;
                    tradeProfitPoints = 0;
                    stop = stop[1];
                    lastTrade = lastTrade[1];
                    entryUnits = 0;
                }
        
            case skipShort:
                if high > shortExit or high > stop[1] then
                {
                    entryUnits = 0;
                    exit = max(open, min(shortExit+oneTick, stop[1]+oneTick));
                    entry = entry[1];
                    trade = trade.flat;
                    tradeProfitPoints = entry[1]-exit;
                    stop = double.nan;
                    lastTrade = 0;
                } 
                else if low < failSafeShortEntry then
                {
                    entry = min(failSafeShortEntry-oneTick, high);
                    exit = 0;
                    trade = trade.short;
                    tradeProfitPoints = 0;
                    stop = entry+ATRMultiplier*atr;
                    entryUnits = unit;
                    lastTrade = 0;
                }
                else
                {
                    exit = 0;
                    entry = entry[1];
                    trade = trade.skipShort;
                    tradeProfitPoints = 0;
                    stop = stop[1];
                    lastTrade = lastTrade[1];
                    entryUnits = 0;
                }
            }
            
    case System_Two:
    
        switch(trade[1]){
    
            case init:
                lastTrade = 0;
                entry = 0;
                exit = 0;
                trade = if barNumber() >=1 then trade.flat else trade.init;
                tradeProfitPoints = 0;
                stop = 0;
                entryUnits = unit;
    
            case flat:
                   if high > failSafeLongEntry then 
                {
                    entry = max(failSafeLongEntry+oneTick, low);
                    exit = 0;
                    trade = trade.long;
                    tradeProfitPoints = 0;
                    stop = entry-ATRMultiplier*atr;
                    entryUnits = unit;
                    lastTrade = 0;
                }
                else if low < failSafeShortEntry then 
                {
                    entry = min(failSafeShortEntry-oneTick, high);
                    exit = 0;
                    trade = trade.short;
                    tradeProfitPoints = 0;
                    stop = entry+ATRMultiplier*atr;
                    entryUnits = unit;
                    lastTrade = 0;
                } 
                else 
                {
                    entry = 0;
                    exit = 0;
                    trade = trade.flat;
                    tradeProfitPoints = 0;
                    stop = double.nan;
                    entryUnits = entryUnits[1];
                    lastTrade = 0;
                }
    
            case long:
                entryUnits = entryUnits[1];
                if low < longExit or low < stop[1] then
                {
                    exit = min(open, max(longExit-oneTick, stop[1]-oneTick));
                    entry = entry[1];
                    trade = trade.flat;
                    tradeProfitPoints = exit - entry[1];
                    stop = double.nan;
                    lastTrade = 0;
                } 
                else
                {
                    exit = 0;
                    entry = entry[1];
                    trade = trade.long;
                    tradeProfitPoints = 0;
                    stop = stop[1];
                    lastTrade = 0;
                }
    
            case short:
                entryUnits = entryUnits[1];
                 if high > shortExit or high > stop[1] then
                {
                    exit = max(open, min(shortExit+oneTick, stop[1]+oneTick));
                    entry = entry[1];
                    trade = trade.flat;
                    tradeProfitPoints = entry[1]-exit;
                    stop = double.nan;
                    lastTrade = 0;
                } 
                else
                {
                    exit = 0;
                    entry = entry[1];
                    trade = trade.short;
                    tradeProfitPoints = 0;
                    stop = stop[1];
                    lastTrade = 0;
                }
                
            case skipShort:
                entryUnits = 0;
                exit = 0;
                entry = 0;
                trade = trade.short;
                tradeProfitPoints = 0;
                stop = 0;
                lastTrade = 0;
    
            case skipLong:
                entryUnits = 0;
                exit = 0;
                entry = 0;
                trade = trade.short;
                tradeProfitPoints = 0;
                stop = 0;
                lastTrade = 0;
        }
    
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
