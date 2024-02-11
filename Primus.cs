using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.DataFeedsCore;
using ATAS.Indicators;
using ATAS.Indicators.Technical;
using ATAS.Strategies.Chart;
using System.Net;
using ATAS.Indicators.Technical.Properties;
using Newtonsoft.Json.Linq;
using static ATAS.Indicators.Technical.SampleProperties;

using Color = System.Drawing.Color;
using MColor = System.Windows.Media.Color;
using MColors = System.Windows.Media.Colors;
using Pen = System.Drawing.Pen;
using String = System.String;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using Utils.Common.Logging;
using System.Diagnostics;

namespace Primus
{
    public class Primus : ChartStrategy
    {
        #region Private fields
        private Order _order = new Order();
        private Stopwatch clock = new Stopwatch();
        private DateTime dtStart = DateTime.Now;
        private String sLastTrade = String.Empty;
        private int iPrevOrderBar = -1;
        private decimal dBreakEvenPrice = 0;
        private int iOrderDirection = -1;

        private const int INFO = 1;
        private const int WARN = 2;
        private const int ERROR = 3;
        private const int LONG = 1;
        private const int SHORT = 2;
        private const int ACTIVE = 1;
        private const int STOPPED = 2;

        private const String sVersion = "1.0";
        private List<string> lsH = new List<string>();
        private List<string> lsM = new List<string>();

        private decimal iJunk = 0;
        private int _lastBar = -1;
        private int _prevBar = -1;
        private int iMinDelta = 0;
        private int iMinDeltaPercent = 0;
        private int iMinADX = 0;
        private int iOffset = 9;
        private int iFontSize = 10;
        private int iNewsFont = 10;
        private int iWaddaSensitivity = 150;
        private int iBotStatus = ACTIVE;
        private int iTotalTrades = 0;

        private bool _lastBarCounted;
        private bool bNewsProcessed = false;
        private bool bShowUp = true;
        private bool bShowDown = true;
        private bool bCloseOnStop = true;

        public decimal Volume = 1;

        #endregion

        #region GENERIC / DISPLAY OPTIONS

        [Display(Name = "Font Size", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(1, 90)]
        public int TextFont { get => iFontSize; set { iFontSize = value; RecalculateValues(); } }

        [Display(Name = "Text Offset", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(0, 900)]
        public int Offset { get => iOffset; set { iOffset = value; RecalculateValues(); } }

        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "UseAlerts")]
        public bool UseAlerts { get; set; }

        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "AlertFile")]
        public string AlertFile { get; set; } = "alert1";

        #endregion

        #region START TRADING OPTIONS

        private bool bEnterBuySell = true;
        private bool bEnterVolImb = true;
        private bool bEnterBBWick = false;
        private bool bEnterHighLow = false;
        private bool bEnter921Cross = false;
        private bool bEnterKamaWick = false;
        private bool bEnterNebula = false;
        private bool bEnterPinbar = false;

        [Display(GroupName = "Begin trading when", Name = "Standard buy/sell signal")]
        public bool EnterBuySell { get => bEnterBuySell; set { bEnterBuySell = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "Volume imbalance (candle gap)")]
        public bool EnterVolImb { get => bEnterVolImb; set { bEnterVolImb = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "Bollinger bands wick")]
        public bool EnterBBWick { get => bEnterBBWick; set { bEnterBBWick = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "High/Low reversal")]
        public bool EnterHighLow { get => bEnterHighLow; set { bEnterHighLow = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "9/21 cross")]
        public bool Enter921Cross { get => bEnter921Cross; set { bEnter921Cross = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "KAMA wick")]
        public bool EnterKamaWick { get => bEnterKamaWick; set { bEnterKamaWick = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "Standard Nebula rules")]
        public bool EnterNebula { get => bEnterNebula; set { bEnterNebula = value; RecalculateValues(); } }
        [Display(GroupName = "Begin trading when", Name = "Enter on pin bar")]
        public bool EnterPinbar { get => bEnterPinbar; set { bEnterPinbar = value; RecalculateValues(); } }

        #endregion

        #region SKIP TRADING OPTIONS

        private bool bSkipBBPushed = false;
        private bool bSkipOSOB = false;

        [Display(GroupName = "Skip trading when", Name = "BB stressed")]
        public bool SkipBBPushed { get => bSkipBBPushed; set { bSkipBBPushed = value; RecalculateValues(); } }
        [Display(GroupName = "Skip trading when", Name = "Oversold or Overbought")]
        public bool SkipOSOB { get => bSkipOSOB; set { bSkipOSOB = value; RecalculateValues(); } }

        #endregion

        #region ADVANCED OPTIONS

        private bool bAdvAddContract = true;
        private bool bAdvAvoidNews = true;
        private int iAdvMaxContracts = 6;
        private int iAdvPauseCandles = 2;
        private int iAdvBETicks = 10;

        [Display(GroupName = "Advanced Options", Name = "Avoid trading near news")]
        public bool AdvAvoidNews { get => bAdvAvoidNews; set { bAdvAvoidNews = value; RecalculateValues(); } }
        [Display(GroupName = "Advanced Options", Name = "Add contract on buy signal")]
        public bool AdvAddContract { get => bAdvAddContract; set { bAdvAddContract = value; RecalculateValues(); } }

        [Display(Name = "Max simultaneous contracts", GroupName = "Advanced Options", Order = int.MaxValue)]
        [Range(1, 90)]
        public int AdvMaxContracts { get => iAdvMaxContracts; set { iAdvMaxContracts = value; RecalculateValues(); } }
        [Display(Name = "Pause candles before new trade", GroupName = "Advanced Options", Order = int.MaxValue)]
        [Range(1, 90)]
        public int AdvPauseCandles { get => iAdvPauseCandles; set { iAdvPauseCandles = value; RecalculateValues(); } }
        [Display(Name = "BreakEven after X ticks", GroupName = "Advanced Options", Order = int.MaxValue)]
        [Range(1, 90)]
        public int AdvBETicks { get => iAdvBETicks; set { iAdvBETicks = value; RecalculateValues(); } }

        #endregion

        #region EXIT OPTIONS

        private bool bExWaddah = true;
        private bool bExHighLow = true;
        private bool bExBBWick = false;
        private bool bExBBEngulf = true;
        private bool bExT3Cross = true;
        private bool bExKAMACross = true;
        private bool bExSqueeze = false;
        private bool bExMajorLine = false;
        private bool bExStairs = false;
        private double dExATR = 1.5;

        [Display(GroupName = "Exit Trade When", Name = "Waddah Explosion color change")]
        public bool ExWaddah { get => bExWaddah; set { bExWaddah = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Bollinger band wick")]
        public bool ExBBWick { get => bExBBWick; set { bExBBWick = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Hard crossing KAMA 9")]
        public bool ExKAMACross { get => bExKAMACross; set { bExKAMACross = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Squeeze relaxer signal")]
        public bool ExSqueeze { get => bExSqueeze; set { bExSqueeze = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Hitting a major line")]
        public bool ExMajorLine { get => bExMajorLine; set { bExMajorLine = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "T3 cross")]
        public bool ExT3Cross { get => bExT3Cross; set { bExT3Cross = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Bollinger band engulfing")]
        public bool ExBBEngulf { get => bExBBEngulf; set { bExBBEngulf = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Declining stairs")]
        public bool ExStairs { get => bExStairs; set { bExStairs = value; RecalculateValues(); } }
        [Display(GroupName = "Exit Trade When", Name = "Equal high/low")]
        public bool ExHighLow { get => bExHighLow; set { bExHighLow = value; RecalculateValues(); } }

        [Display(Name = "ATR exceeds value", GroupName = "Exit Trade When", Order = int.MaxValue)]
        public double ExATR { get => dExATR; set { dExATR = value; RecalculateValues(); } }


        #endregion

        #region INDICATORS

        private readonly RSI _rsi = new() { Period = 14 };
        private readonly ATR _atr = new() { Period = 14 };
        private readonly AwesomeOscillator _ao = new AwesomeOscillator();
        private readonly ParabolicSAR _psar = new ParabolicSAR();
        private readonly ADX _adx = new ADX() { Period = 10 };
        private readonly EMA _9 = new EMA() { Period = 9 };
        private readonly EMA _21 = new EMA() { Period = 21 };
        private readonly EMA fastEma = new EMA() { Period = 20 };
        private readonly EMA slowEma = new EMA() { Period = 40 };
        private readonly FisherTransform _ft = new FisherTransform() { Period = 10 };
        private readonly SuperTrend _st = new SuperTrend() { Period = 10, Multiplier = 1m };
        private readonly BollingerBands _bb = new BollingerBands() { Period = 20, Shift = 0, Width = 2 };
        private readonly KAMA _kama9 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 9 };
        private readonly KAMA _kama21 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 21 };
        private readonly MACD _macd = new MACD() { ShortPeriod = 12, LongPeriod = 26, SignalPeriod = 9 };
        private readonly T3 _t3 = new T3() { Period = 10, Multiplier = 1 };
        private readonly SqueezeMomentum _sq = new SqueezeMomentum() { BBPeriod = 20, BBMultFactor = 2, KCPeriod = 20, KCMultFactor = 1.5m, UseTrueRange = false };

        #endregion

        #region BUY SELL FILTERS

        // Default TRUE
        private bool bUseFisher = true;          // USE
        private bool bUseWaddah = true;
        private bool bUseT3 = true;
        private bool bUseSuperTrend = true;
        private bool bUseAO = true;
        private bool bUsePSAR = true;
        private bool bVolumeImbalances = true;

        // Default FALSE
        private bool bUseSqueeze = false;
        private bool bUseMACD = false;
        private bool bUseKAMA = false;

        [Display(GroupName = "Buy/Sell Filters", Name = "Waddah Explosion", Description = "The Waddah Explosion must be the correct color, and have a value")]
        public bool Use_Waddah_Explosion { get => bUseWaddah; set { bUseWaddah = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Awesome Oscillator", Description = "AO is positive or negative")]
        public bool Use_Awesome { get => bUseAO; set { bUseAO = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Parabolic SAR", Description = "The PSAR must be signaling a buy/sell signal same as the arrow")]
        public bool Use_PSAR { get => bUsePSAR; set { bUsePSAR = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Squeeze Momentum", Description = "The squeeze must be the correct color")]
        public bool Use_Squeeze_Momentum { get => bUseSqueeze; set { bUseSqueeze = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "MACD", Description = "Standard 12/26/9 MACD crossing in the correct direction")]
        public bool Use_MACD { get => bUseMACD; set { bUseMACD = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "SuperTrend", Description = "Price must align to the current SuperTrend trend")]
        public bool Use_SuperTrend { get => bUseSuperTrend; set { bUseSuperTrend = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "T3", Description = "Price must cross the T3")]
        public bool Use_T3 { get => bUseT3; set { bUseT3 = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Fisher Transform", Description = "Fisher Transform must cross to the correct direction")]
        public bool Use_Fisher_Transform { get => bUseFisher; set { bUseFisher = value; RecalculateValues(); } }

        [Display(GroupName = "Value Filters", Name = "Minimum Delta", Description = "The minimum candle delta value to show buy/sell")]
        [Range(0, 9000)]
        public int Min_Delta { get => iMinDelta; set { if (value < 0) return; iMinDelta = value; RecalculateValues(); } }

        [Display(GroupName = "Value Filters", Name = "Minimum Delta Percent", Description = "Minimum diff between max delta and delta to show buy/sell")]
        [Range(0, 100)]
        public int Min_Delta_Percent { get => iMinDeltaPercent; set { if (value < 0) return; iMinDeltaPercent = value; RecalculateValues(); } }

        [Display(GroupName = "Value Filters", Name = "Minimum ADX", Description = "Minimum ADX value before showing buy/sell")]
        [Range(0, 100)]
        public int Min_ADX { get => iMinADX; set { if (value < 0) return; iMinADX = value; RecalculateValues(); } }

        #endregion

        #region Stock HTTP Fetch
        private void ParseStockEvents(String result, int bar)
        {
            int iJSONStart = 0;
            int iJSONEnd = -1;
            String sFinalText = String.Empty; String sNews = String.Empty; String name = String.Empty; String impact = String.Empty; String time = String.Empty; String actual = String.Empty; String previous = String.Empty; String forecast = String.Empty;

            try
            {
                iJSONStart = result.IndexOf("window.calendarComponentStates[1] = ");
                iJSONEnd = result.IndexOf("\"}]}],", iJSONStart);
                sFinalText = result.Substring(iJSONStart, iJSONEnd - iJSONStart);
                sFinalText = sFinalText.Replace("window.calendarComponentStates[1] = ", "");
                sFinalText += "\"}]}]}";

                var jsFile = JObject.Parse(sFinalText);
                foreach (JToken j3 in (JArray)jsFile["days"])
                {
                    JToken j2 = j3.SelectToken("events");
                    foreach (JToken j in j2)
                    {
                        name = j["name"].ToString();
                        impact = j["impactTitle"].ToString();
                        time = j["timeLabel"].ToString();
                        actual = j["actual"].ToString();
                        previous = j["previous"].ToString();
                        forecast = j["forecast"].ToString();
                        sNews = time + "     " + name;
                        if (previous.ToString().Trim().Length > 0)
                            sNews += " (Prev: " + previous + ", Forecast: " + forecast + ")";
                        if (impact.Contains("High"))
                            lsH.Add(sNews);
                        if (impact.Contains("Medium"))
                            lsM.Add(sNews);
                    }
                }
            }
            catch { }
        }

        private void LoadStock(int bar)
        {
            try
            {
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create("https://www.forexfactory.com/calendar?day=today");
                myRequest.Method = "GET";
                myRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.150 Safari/537.36";
                WebResponse myResponse = myRequest.GetResponse();
                StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);
                string result = sr.ReadToEnd();
                sr.Close();
                myResponse.Close();
                ParseStockEvents(result, bar);
                bNewsProcessed = true;
            }
            catch { }
        }
        #endregion

        #region CONSTRUCTOR

        public Primus()
        {
            bCloseOnStop = true;
            EnableCustomDrawing = true;
        }

        #endregion

        #region RENDER CONTEXT

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            var font = new RenderFont("Arial", iNewsFont);
            var fontB = new RenderFont("Arial", iNewsFont, FontStyle.Bold);
            int upY = 50;
            int upX = 50;
            var txt = String.Empty;

            // LINE 1 - BOT STATUS + ACCOUNT + START TIME
            switch (iBotStatus)
            {
                case ACTIVE:
                    TimeSpan t = TimeSpan.FromMilliseconds(clock.ElapsedMilliseconds);
                    String an = String.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
                    txt = $"BOT ACTIVE on {TradingManager.Portfolio.AccountID} since " + dtStart.ToString() + " (" + an + ")";
                    context.DrawString(txt, fontB, Color.Lime, upX, upY);
                    if (!clock.IsRunning)
                        clock.Start();
                    break;
                case STOPPED:
                    txt = $"BOT STOPPED on {TradingManager.Portfolio.AccountID}";
                    context.DrawString(txt, fontB, Color.Orange, upX, upY);
                    if (clock.IsRunning)
                        clock.Stop();
                    break;
            }
            var tsize = context.MeasureString(txt, fontB);
            upY += tsize.Height + 6;

            // LINE 2 - TOTAL TRADES + PNL
            if (TradingManager.Portfolio != null && TradingManager.Position != null)
            {
                txt = $"{TradingManager.MyTrades.Count()} trades, with PNL: {TradingManager.Position.RealizedPnL}";
                context.DrawString(txt, font, Color.White, upX, upY);
                upY += tsize.Height + 6;
                txt = sLastTrade;
                context.DrawString(txt, font, Color.White, upX, upY);
            }


        }

        #endregion

        #region MAIN LOGIC

        protected override void OnCalculate(int bar, decimal value)
        {
            var pbar = bar - 1;
            var prevBar = _prevBar;
            _prevBar = bar;

            if (prevBar == bar)
                return;

            #region CANDLE CALCULATIONS

            var candle = GetCandle(pbar);
            value = candle.Close;
            var chT = ChartInfo.ChartType;
            var red = candle.Close < candle.Open;
            var green = candle.Close > candle.Open;

            bShowDown = true;
            bShowUp = true;

            decimal _tick = ChartInfo.PriceChartContainer.Step;
            var p1C = GetCandle(pbar - 1);
            var p2C = GetCandle(pbar - 2);
            var p3C = GetCandle(pbar - 3);
            var p4C = GetCandle(pbar - 4);

            var c0G = candle.Open < candle.Close;
            var c0R = candle.Open > candle.Close;
            var c1G = p1C.Open < p1C.Close;
            var c1R = p1C.Open > p1C.Close;
            var c2G = p2C.Open < p2C.Close;
            var c2R = p2C.Open > p2C.Close;
            var c3G = p3C.Open < p3C.Close;
            var c3R = p3C.Open > p3C.Close;
            var c4G = p4C.Open < p4C.Close;
            var c4R = p4C.Open > p4C.Close;

            var c0Body = Math.Abs(candle.Close - candle.Open);
            var c1Body = Math.Abs(p1C.Close - p1C.Open);
            var c2Body = Math.Abs(p2C.Close - p2C.Open);
            var c3Body = Math.Abs(p3C.Close - p3C.Open);
            var c4Body = Math.Abs(p4C.Close - p4C.Open);

            var upWick50PerLarger = c0R && Math.Abs(candle.High - candle.Open) > Math.Abs(candle.Low - candle.Close);
            var downWick50PerLarger = c0G && Math.Abs(candle.Low - candle.Open) > Math.Abs(candle.Close - candle.High);

            decimal deltaPer = 0;
            if (candle.Delta > 0 && candle.MaxDelta > 0)
            {
                if (candle.MinDelta > 0)
                    deltaPer = (candle.Delta / candle.MaxDelta) * 100;
                else
                    deltaPer = (candle.Delta / candle.MinDelta) * 100;
            }

            #endregion

            #region INDICATOR CALCULATIONS

            _t3.Calculate(pbar, value);
            fastEma.Calculate(pbar, value);
            slowEma.Calculate(pbar, value);
            _21.Calculate(pbar, value);
            _macd.Calculate(pbar, value);
            _bb.Calculate(pbar, value);
            _rsi.Calculate(pbar, value);

            var ao = ((ValueDataSeries)_ao.DataSeries[0])[pbar];
            var kama9 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
            var kama21 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
            var m1 = ((ValueDataSeries)_macd.DataSeries[0])[pbar];
            var m2 = ((ValueDataSeries)_macd.DataSeries[1])[pbar];
            var m3 = ((ValueDataSeries)_macd.DataSeries[2])[pbar];
            var t3 = ((ValueDataSeries)_t3.DataSeries[0])[pbar];
            var fast = ((ValueDataSeries)fastEma.DataSeries[0])[pbar];
            var fastM = ((ValueDataSeries)fastEma.DataSeries[0])[pbar - 1];
            var slow = ((ValueDataSeries)slowEma.DataSeries[0])[pbar];
            var slowM = ((ValueDataSeries)slowEma.DataSeries[0])[pbar - 1];
            var sq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar];
            var sq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar];
            var psq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar - 1];
            var psq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar - 1];
            var ppsq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar - 2];
            var ppsq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar - 2];
            var f1 = ((ValueDataSeries)_ft.DataSeries[0])[pbar];
            var f2 = ((ValueDataSeries)_ft.DataSeries[1])[pbar];
            var st = ((ValueDataSeries)_st.DataSeries[0])[pbar];
            var x = ((ValueDataSeries)_adx.DataSeries[0])[pbar];
            var nn = ((ValueDataSeries)_9.DataSeries[0])[pbar];
            var prev_nn = ((ValueDataSeries)_9.DataSeries[0])[pbar - 1];
            var twone = ((ValueDataSeries)_21.DataSeries[0])[pbar];
            var prev_twone = ((ValueDataSeries)_21.DataSeries[0])[pbar - 1];
            var psar = ((ValueDataSeries)_psar.DataSeries[0])[pbar];
            var bb_mid = ((ValueDataSeries)_bb.DataSeries[0])[pbar]; // mid
            var bb_top = ((ValueDataSeries)_bb.DataSeries[1])[pbar]; // top
            var bb_bottom = ((ValueDataSeries)_bb.DataSeries[2])[pbar]; // bottom
            var rsi = ((ValueDataSeries)_rsi.DataSeries[0])[pbar];
            var rsi1 = ((ValueDataSeries)_rsi.DataSeries[0])[pbar - 1];
            var rsi2 = ((ValueDataSeries)_rsi.DataSeries[0])[pbar - 2];

            var t1 = ((fast - slow) - (fastM - slowM)) * iWaddaSensitivity;

            var fisherUp = (f1 < f2);
            var fisherDown = (f2 < f1);
            var macdUp = (m1 > m2);
            var macdDown = (m1 < m2);
            var psarBuy = (psar < candle.Close);
            var psarSell = (psar > candle.Close);

            var atr = _atr[bar];
            var median = (candle.Low + candle.High) / 2;
            var dUpperLevel = median + atr * 1.7m;
            var dLowerLevel = median - atr * 1.7m;

            #endregion

            #region BAR PATTERN CALCULATIONS

            var ThreeOutUp = c2R && c1G && c0G && p1C.Open < p2C.Close && p2C.Open < p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close > p1C.Low;

            var ThreeOutDown = c2G && c1R && c0R && p1C.Open > p2C.Close && p2C.Open > p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close < p1C.Low;

            var eqHigh = c0R && c1R && c2G && c3G && candle.Close < p1C.Close && (p1C.Open == p2C.Close || p1C.Open == p2C.Close + _tick || p1C.Open + _tick == p2C.Close);

            var eqLow = c0G && c1G && c2R && c3R && candle.Close > p1C.Close && (p1C.Close == p2C.Open || p1C.Close == p2C.Open + _tick || p1C.Close + _tick == p2C.Open);

            // Squeeze momentum relaxer show
            if (sq1 > 0 && sq1 < psq1 && psq1 > ppsq1)
                iJunk = 9;
            if (sq1 < 0 && sq1 > psq1 && psq1 < ppsq1)
                iJunk = 9;

            if (c0G && c1R && c2R && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta < 0 && candle.Close > 0)
                DrawText(bar, "Vol\nRev", Color.Yellow, Color.Transparent, false, true);
            if (c0R && c1G && c2G && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta > 0 && candle.Close > 0)
                DrawText(bar, "Vol\nRev", Color.Lime, Color.Transparent, false, true);

            if (deltaPer < iMinDeltaPercent)
            {
                bShowUp = false;
                bShowDown = false;
            }

            // Standard BUY / SELL
            if ((candle.Delta < iMinDelta) || (!macdUp && bUseMACD) || (psarSell && bUsePSAR) || (!fisherUp && bUseFisher) || (value < t3 && bUseT3) || (value < kama9 && bUseKAMA) || (t1 < 0 && bUseWaddah) || (ao < 0 && bUseAO) || (st == 0 && bUseSuperTrend) || (sq1 < 0 && bUseSqueeze) || (x < iMinADX))
                bShowUp = false;

            if ((candle.Delta > (iMinDelta * -1)) || (psarBuy && bUsePSAR) || (!macdDown && bUseMACD) || (!fisherDown && bUseFisher) || (value > kama9 && bUseKAMA) || (value > t3 && bUseT3) || (t1 >= 0 && bUseWaddah) || (ao > 0 && bUseAO) || (st == 0 && bUseSuperTrend) || (sq1 > 0 && bUseSqueeze) || (x < iMinADX))
                bShowDown = false;

            #endregion

            #region ALERTS

            if (_lastBar != bar)
            {
                if (_lastBarCounted)
                {
                    if (bVolumeImbalances)
                        if ((green && c1G && candle.Open > p1C.Close) || (red && c1R && candle.Open < p1C.Close))
                            AddAlert(AlertFile, "Volume Imbalance");

                    if (bShowUp)
                        AddAlert(AlertFile, "BUY Signal");
                    else if (bShowDown)
                        AddAlert(AlertFile, "BUY Signal");
                }
                _lastBar = bar;
            }
            else
            {
                if (!_lastBarCounted)
                    _lastBarCounted = true;
            }

            #endregion

            #region REVERSAL PATTERNS

            var bbWickLong = false;
            var bbWickShort = false;
            // Bollinger band bounce
            if (candle.Low < bb_bottom && candle.Open > bb_bottom && c0G && candle.Close > p1C.Close && downWick50PerLarger)
                bbWickLong = true;
            if (candle.High > bb_top && candle.Open < bb_top && c0R && candle.Close < p1C.Close && upWick50PerLarger)
                bbWickShort = true;

            if (ThreeOutUp)
                DrawText(bar, "3oU", Color.Yellow, Color.Transparent);
            if (ThreeOutDown)
                DrawText(bar, "3oD", Color.Yellow, Color.Transparent);

            // Trampoline
            if (c0R && c1R && candle.Close < p1C.Close && (rsi >= 70 || rsi1 >= 70 || rsi2 >= 70) &&
                c2G && p2C.High >= (bb_top - (_tick * 30)))
                DrawText(bar, "TR", Color.Yellow, Color.BlueViolet);
            if (c0G && c1G && candle.Close > p1C.Close && (rsi < 25 || rsi1 < 25 || rsi2 < 25) &&
                c2R && p2C.Low <= (bb_bottom + (_tick * 30)))
                DrawText(bar - 2, "TR", Color.Yellow, Color.BlueViolet);

            #endregion

            #region EXIT STRATEGIES

            if (TradingManager.Position is not null)
            {
                var dir = CurrentPosition > 0 ? LONG : SHORT;

                if (bEnterBBWick && (bbWickLong || bbWickShort))
                    CloseCurrentPosition("Bollinger band wick EXIT");

                if (bExHighLow && (eqHigh || eqLow))
                    CloseCurrentPosition("Equal high/low EXIT");

                if (candle.Close < t3 && red && bExT3Cross && dir == LONG)
                    CloseCurrentPosition("T3 cross EXIT");
                if (candle.Close > t3 && green && bExT3Cross && dir == SHORT)
                    CloseCurrentPosition("T3 cross EXIT");

                if (candle.Close < kama9 && red && bExKAMACross && dir == LONG)
                    CloseCurrentPosition("9 KAMA cross EXIT");
                if (candle.Close > kama9 && green && bExKAMACross && dir == SHORT)
                    CloseCurrentPosition("9 KAMA cross EXIT");

                // Go break even after X ticks
                if (Math.Abs(dBreakEvenPrice - Security.BestBidPrice) > (_tick * iAdvBETicks))
                    iJunk = 0; // LimitAtBE();

            }

            #endregion

            #region ENTRANCE STRATEGIES

            if (red && bShowDown)
                OpenPosition("Standard Sell Signal", candle, bar, SHORT);
            if (green && bShowUp)
                OpenPosition("Standard Buy Signal", candle, bar, LONG);

            if (green && c1G && candle.Open > p1C.Close && bEnterVolImb)
                OpenPosition("Volume Imbalance", candle, bar, LONG);
            if (red && c1R && candle.Open < p1C.Close)
                OpenPosition("Volume Imbalance", candle, bar, SHORT);

            if (eqHigh && bEnterHighLow)
                OpenPosition("Equal High", candle, bar, SHORT);
            if (eqLow && bEnterHighLow)
                OpenPosition("Equal Low", candle, bar, LONG);

            if (nn > twone && prev_nn <= prev_twone && bEnter921Cross)
                OpenPosition("9/21 cross", candle, bar, LONG);
            if (nn < twone && prev_nn >= prev_twone && bEnter921Cross)
                OpenPosition("9/21 cross", candle, bar, SHORT);

            if (bEnterBBWick && bbWickLong)
                OpenPosition("Bollinger Band Wick", candle, bar, LONG);
            if (bEnterBBWick && bbWickShort)
                OpenPosition("Bollinger Band Wick", candle, bar, SHORT);

            if (green && candle.Low < kama9 && candle.Close > kama9 && bEnterKamaWick)
                OpenPosition("9 KAMA Wick", candle, bar, LONG);
            if (red && candle.High > kama9 && candle.Close < kama9 && bEnterKamaWick)
                OpenPosition("9 KAMA Wick", candle, bar, SHORT);

            #endregion

            if (!bNewsProcessed)
                LoadStock(bar);
        }

        #endregion

        #region POSITION METHODS

        private void OpenPosition(String sReason, IndicatorCandle c, int bar, int iDirection = -1)
        {
            // Limit 1 order per bar
            if (iPrevOrderBar == bar)
                return;
            else
                iPrevOrderBar = bar;

            if (iDirection == LONG && iOrderDirection == SHORT && Math.Abs(CurrentPosition) > 0)
                CloseCurrentPosition("Opposite direction order - cancelling current");
            if (iDirection == SHORT && iOrderDirection == LONG && Math.Abs(CurrentPosition) > 0)
                CloseCurrentPosition("Opposite direction order - cancelling current");

            OrderDirections d = OrderDirections.Buy;

            if (c.Open > c.Close || iDirection == SHORT)
                d = OrderDirections.Sell;
            if (c.Open < c.Close || iDirection == LONG)
                d = OrderDirections.Buy;

            _order = new Order
            {
                Portfolio = Portfolio,
                Security = Security,
                Direction = d,
                Type = OrderTypes.Market,
                QuantityToFill = 1, // GetOrderVolume(),
                Comment = "Bar " + bar + " - " + sReason
            };

            if (iDirection == LONG)
                sLastTrade = "Bar " + bar + " - " + sReason + " LONG at " + c.Close;
            else
                sLastTrade = "Bar " + bar + " - " + sReason + " SHORT at " + c.Close;

            OpenOrder(_order);
            iOrderDirection = iDirection;
        }

        private void CloseCurrentPosition(String s)
        {
            if (Math.Abs(CurrentPosition) > 0)
            {
                _order = new Order
                {
                    Portfolio = Portfolio,
                    Security = Security,
                    Direction = CurrentPosition > 0 ? OrderDirections.Sell : OrderDirections.Buy,
                    Type = OrderTypes.Market,
                    QuantityToFill = Math.Abs(CurrentPosition),
                    Comment = s
                };
                OpenOrder(_order);
                iOrderDirection = -1;
            }
        }

        private void LimitAtBE()
        {
            var iBEDirection = OrderDirections.Buy;
            var currDir = CurrentPosition > 0 ? LONG : SHORT;
            if (currDir == LONG)
                iBEDirection = OrderDirections.Sell;

            var order = new Order
            {
                Portfolio = Portfolio,
                Security = Security,
                Direction = CurrentPosition > 0 ? OrderDirections.Sell : OrderDirections.Buy,
                Type = OrderTypes.Limit,
                QuantityToFill = Math.Abs(CurrentPosition),
                Price = dBreakEvenPrice,
                Comment = "Profit reached at " + iAdvBETicks + " ticks, break even open"
            };
            OpenOrder(order);
        }

        private decimal GetOrderVolume()
        {
            if (CurrentPosition == 0)
                return Volume;
            if (CurrentPosition > 0)
                return CurrentPosition;

            return Volume + Math.Abs(CurrentPosition);
        }

        protected override void OnOrderRegisterFailed(Order order, string message)
        {
            if (order == _order)
            {
                AddLog("ORDER FAILED: " + message, ERROR);
            }
        }

        protected override void OnOrderChanged(Order order)
        {
            if (order == _order)
            {
                switch (order.Status())
                {
                    case OrderStatus.None:
                        // The order has an undefined status (you need to wait for the next method calls).
                        break;
                    case OrderStatus.Placed:
                        // the order is placed.
                        break;
                    case OrderStatus.Filled:
                        // the order is filled.
                        break;
                    case OrderStatus.PartlyFilled:
                        // the order is partially filled.
                        {
                            var unfilled = order.Unfilled; // this is a unfilled volume.

                            break;
                        }
                    case OrderStatus.Canceled:
                        // the order is canceled.
                        break;
                }
            }
        }

        protected override void OnStopping()
        {
            if (CurrentPosition != 0 && bCloseOnStop)
            {
                RaiseShowNotification($"Closing position {CurrentPosition} on stopping.");
                CloseCurrentPosition($"Closing position {CurrentPosition} on stopping.");
            }

            base.OnStopping();
        }

        #endregion

        #region MISC METHODS

        private void AddLog(String s, int iSev = INFO)
        {
            switch (iSev)
            {
                case WARN: this.LogInfo(s); break;
                case ERROR: this.LogWarn(s); break;
                default: this.LogError(s); break;
            }
        }

        private decimal VolSec(IndicatorCandle c) { return c.Volume / Convert.ToDecimal((c.LastTime - c.Time).TotalSeconds); }

        protected void DrawText(int bBar, String strX, Color cI, Color cB, bool bOverride = false, bool bSwap = false)
        {
        }

            #endregion

        }
    }
