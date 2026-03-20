using QuickFix;
using QuickFix.Fields;
using System;

namespace QuickFix.FIX50SP2;

public class PriceReference : Message
{
    public const string MsgType = "pr";

    public PriceReference() : base()
    {
        this.Header.SetField(new MsgType(MsgType));
    }

    //// ApplicationSequenceControl
    //public void SetApplID(string value) => SetField(new ApplID(value)); // Tag 1180
    //public void SetApplSeqNum(ulong value) => SetField(new ApplSeqNum(value)); // Tag 1181
    //public void SetApplLastSeqNum(ulong value) => SetField(new ApplLastSeqNum(value)); // Tag 1350

    //// Instrument
    //public void SetSymbol(string value) => SetField(new Symbol(value)); // Tag 55
    //public void SetSecurityID(string value) => SetField(new SecurityID(value)); // Tag 48
    //public void SetSecurityIDSource(string value) => SetField(new SecurityIDSource(value)); // Tag 22

    //// Control
    //public void SetUnsolicitedIndicator(bool value) => SetField(new UnsolicitedIndicator(value)); // Tag 325

    //// Prices
    //public void SetLowLimitPrice(decimal value) => SetField(new LowLimitPrice(value)); // Tag 1148
    //public void SetHighLimitPrice(decimal value) => SetField(new HighLimitPrice(value)); // Tag 1149
    //public void SetTradingReferencePrice(decimal value) => SetField(new TradingReferencePrice(value)); // Tag 1150
    //public void SetBasePrice(decimal value) => SetField(new DecimalField(21003, value)); // Custom tag
    //public void SetTheoreticalPrice(decimal value) => SetField(new DecimalField(21025, value)); // Custom tag
    //public void SetPrevClosePx(decimal value) => SetField(new PrevClosePx(value)); // Tag 140

    //// Time
    //public void SetTransactTime(DateTime value) => SetField(new TransactTime(value)); // Tag 60
}
