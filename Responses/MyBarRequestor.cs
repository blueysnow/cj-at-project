using System;
using System.Collections.Generic;
using System.Text;
using TradeLink.API;
using TradeLink.Common;

namespace Responses
{
    /// <summary>
    /// demonstrates response that uses historical bar data
    /// </summary>
    public class MyBarRequestor : ResponseTemplate
    {
        MessageTracker _mt = new MessageTracker();
        BarListTracker _blt = new BarListTracker(BarInterval.Day);

        int barRequestedFlag = 0;

        public override void Reset()
        {
            _mt.BLT = _blt;
        }

        public override void GotTick(Tick k)
        {
            // if we don't have bar data, request historical data
            if (_blt[k.symbol].Count == 0)
            {
                if (barRequestedFlag == 0 )
                {
                    D(k.symbol + " no bars found, requesting...");
                    sendmessage(MessageTypes.BARREQUEST, BarImpl.BuildBarRequest(k.symbol, BarInterval.Day, 20120105));
                    barRequestedFlag += 1;
                }
                else
                    D("Waiting for Broker backfill");
            }
            D(k.symbol + " bar count: " + _blt[k.symbol].Count);
            // update whatever data we have with ticks
            _blt.newTick(k);

        }

        public override void GotMessage(MessageTypes type, long source, long dest, long msgid, string request, ref string response)
        {
            if (type == MessageTypes.BARRESPONSE)
                D(response);
            _mt.GotMessage(type, source, dest, msgid, request, ref response);
        }

    }
}
