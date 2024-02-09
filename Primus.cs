using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.DataFeedsCore;
using ATAS.Indicators;
using ATAS.Indicators.Technical;
using ATAS.Strategies.Chart;
using System.Net;
using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;
using Newtonsoft.Json.Linq;
using static ATAS.Indicators.Technical.SampleProperties;

using Color = System.Drawing.Color;
using MColor = System.Windows.Media.Color;
using MColors = System.Windows.Media.Colors;
using Pen = System.Drawing.Pen;
using String = System.String;

namespace Primus
{
    public class Primus : ChartStrategy
    {
        #region Private fields

        private const String sVersion = "1.0";
        private List<string> lsH = new List<string>();
        private List<string> lsM = new List<string>();

        private decimal iJunk = 0;
        private int _lastBar;
        private int iMinDelta = 0;
        private int iMinDeltaPercent = 0;
        private int iMinADX = 0;
        private int iOffset = 9;
        private int iFontSize = 10;
        private int iNewsFont = 10;
        private int iWaddaSensitivity = 120;

        private bool _lastBarCounted;
        private bool bNewsProcessed = false;
        private bool bShowUp = true;
        private bool bShowDown = true;
        private bool bCloseOnStop = true;

        public decimal Volume = 0;

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
        private bool bEnterBBWick = true;
        private bool bEnterHighLow = true;
        private bool bEnter921Cross = true;
        private bool bEnterKamaWick = true;
        private bool bEnterNebula = true;

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

        #endregion

        #region SKIP TRADING OPTIONS

        private bool bSkipBBPushed = true;
        private bool bSkipOSOB = true;

        [Display(GroupName = "Skip trading when", Name = "")]
        public bool SkipBBPushed { get => bSkipBBPushed; set { bSkipBBPushed = value; RecalculateValues(); } }
        [Display(GroupName = "Skip trading when", Name = "")]
        public bool SkipOSOB { get => bSkipOSOB; set { bSkipOSOB = value; RecalculateValues(); } }

        #endregion

        #region ADVANCED OPTIONS

        private bool bAdvAddContract = true;
        private bool bAdvAvoidNews = true;
        private int iAdvMaxContracts = 2;
        private int iAdvPauseCandles = 2;

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
            var firstSeries = (ValueDataSeries)DataSeries[0];
            firstSeries.Name = "Short";
            firstSeries.Color = MColors.Red;
            firstSeries.VisualType = VisualMode.Line;

            DataSeries.Add(new ValueDataSeries("Long")
            {
                VisualType = VisualMode.Line,
                Color = MColors.Green,
            });

            bCloseOnStop = true;
        }

        #endregion

        #region Main Logic

        // ========================================================================
        // =========================    MAIN LOGIC      ===========================
        // ========================================================================

        protected override void OnCalculate(int bar, decimal value)
        {
            var candle = GetCandle(bar);
            value = candle.Close;
            var chT = ChartInfo.ChartType;
            var prevBar = _lastBar;
            _lastBar = bar;

            if (bar == 0)
            {
                DataSeries.ForEach(x => x.Clear());
                _lastBarCounted = false;
                return;
            }
            if (bar < 5)
                return;

            if (chT == "Tick" && candle.Ticks < 1500)
                return;

            if (!CanProcess(bar) || prevBar == bar)
                return;

            decimal _tick = ChartInfo.PriceChartContainer.Step;
            var red = candle.Close < candle.Open;
            var green = candle.Close > candle.Open;
            var p1C = GetCandle(bar - 1);
            var c1G = p1C.Open < p1C.Close;
            var c1R = p1C.Open > p1C.Close;

                var highPen = new Pen(new SolidBrush(Color.RebeccaPurple)) { Width = 2 };
                if (green && c1G && candle.Open > p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(bar, candle.Open, highPen));
                }
                if (red && c1R && candle.Open < p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(bar, candle.Open, highPen));
                }

            bShowDown = true;
            bShowUp = true;

            var p2C = GetCandle(bar - 2);
            var p3C = GetCandle(bar - 3);
            var p4C = GetCandle(bar - 4);

            var c0G = candle.Open < candle.Close;
            var c0R = candle.Open > candle.Close;
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

            var ThreeOutUp = c2R && c1G && c0G && p1C.Open < p2C.Close && p2C.Open < p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close > p1C.Low;

            var ThreeOutDown = c2G && c1R && c0R && p1C.Open > p2C.Close && p2C.Open > p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close < p1C.Low;

            _t3.Calculate(bar, value);
            fastEma.Calculate(bar, value);
            slowEma.Calculate(bar, value);
            _21.Calculate(bar, value);
            _macd.Calculate(bar, value);
            _bb.Calculate(bar, value);
            _rsi.Calculate(bar, value);

            var deltaPer = candle.Delta > 0 ? (candle.Delta / candle.MaxDelta) * 100 : (candle.Delta / candle.MinDelta) * 100;

            // ========================================================================
            // ========================    SERIES FETCH    ============================
            // ========================================================================

            var ao = ((ValueDataSeries)_ao.DataSeries[0])[bar];
            var kama9 = ((ValueDataSeries)_kama9.DataSeries[0])[bar];
            var kama21 = ((ValueDataSeries)_kama9.DataSeries[0])[bar];
            var m1 = ((ValueDataSeries)_macd.DataSeries[0])[bar];
            var m2 = ((ValueDataSeries)_macd.DataSeries[1])[bar];
            var m3 = ((ValueDataSeries)_macd.DataSeries[2])[bar];
            var t3 = ((ValueDataSeries)_t3.DataSeries[0])[bar];
            var fast = ((ValueDataSeries)fastEma.DataSeries[0])[bar];
            var fastM = ((ValueDataSeries)fastEma.DataSeries[0])[bar - 1];
            var slow = ((ValueDataSeries)slowEma.DataSeries[0])[bar];
            var slowM = ((ValueDataSeries)slowEma.DataSeries[0])[bar - 1];
            var sq1 = ((ValueDataSeries)_sq.DataSeries[0])[bar];
            var sq2 = ((ValueDataSeries)_sq.DataSeries[1])[bar];
            var psq1 = ((ValueDataSeries)_sq.DataSeries[0])[bar - 1];
            var psq2 = ((ValueDataSeries)_sq.DataSeries[1])[bar - 1];
            var ppsq1 = ((ValueDataSeries)_sq.DataSeries[0])[bar - 2];
            var ppsq2 = ((ValueDataSeries)_sq.DataSeries[1])[bar - 2];
            var f1 = ((ValueDataSeries)_ft.DataSeries[0])[bar];
            var f2 = ((ValueDataSeries)_ft.DataSeries[1])[bar];
            var st = ((ValueDataSeries)_st.DataSeries[0])[bar];
            var x = ((ValueDataSeries)_adx.DataSeries[0])[bar];
            var nn = ((ValueDataSeries)_9.DataSeries[0])[bar];
            var prev_nn = ((ValueDataSeries)_9.DataSeries[0])[bar - 1];
            var twone = ((ValueDataSeries)_21.DataSeries[0])[bar];
            var prev_twone = ((ValueDataSeries)_21.DataSeries[0])[bar - 1];
            var psar = ((ValueDataSeries)_psar.DataSeries[0])[bar];
            var bb_mid = ((ValueDataSeries)_bb.DataSeries[0])[bar]; // mid
            var bb_top = ((ValueDataSeries)_bb.DataSeries[1])[bar]; // top
            var bb_bottom = ((ValueDataSeries)_bb.DataSeries[2])[bar]; // bottom
            var rsi = ((ValueDataSeries)_rsi.DataSeries[0])[bar];
            var rsi1 = ((ValueDataSeries)_rsi.DataSeries[0])[bar - 1];
            var rsi2 = ((ValueDataSeries)_rsi.DataSeries[0])[bar - 2];

            var eqHigh = c0R && c1R && c2G && c3G &&
                candle.Close < p1C.Close &&
                (p1C.Close == p2C.Open || p1C.Close == p2C.Open + _tick || p1C.Close + _tick == p2C.Open);

            var eqLow = c0G && c1G && c2R && c3R &&
                candle.Close > p1C.Close &&
                (p1C.Close == p2C.Open || p1C.Close == p2C.Open + _tick || p1C.Close + _tick == p2C.Open);

            var t1 = ((fast - slow) - (fastM - slowM)) * iWaddaSensitivity;

            var fisherUp = (f1 < f2);
            var fisherDown = (f2 < f1);
            var macdUp = (m1 > m2);
            var macdDown = (m1 < m2);
            var psarBuy = (psar < candle.Close);
            var psarSell = (psar > candle.Close);

            // ========================================================================
            // ====================    SHOW/OTHER CONDITIONS    =======================
            // ========================================================================

            if (c4Body > c3Body && c3Body > c2Body && c2Body > c1Body && c1Body > c0Body)
                if ((candle.Close > p1C.Close && p1C.Close > p2C.Close && p2C.Close > p3C.Close) ||
                (candle.Close < p1C.Close && p1C.Close < p2C.Close && p2C.Close < p3C.Close))
                    DrawText(bar, "Stairs", Color.Yellow, Color.Transparent);

            if (deltaPer < iMinDeltaPercent)
            {
                bShowUp = false;
                bShowDown = false;
            }

            var atr = _atr[bar];
            var median = (candle.Low + candle.High) / 2;
            var dUpperLevel = median + atr * 1.7m;
            var dLowerLevel = median - atr * 1.7m;

            // Squeeze momentum relaxer show
            if (sq1 > 0 && sq1 < psq1 && psq1 > ppsq1)
                iJunk = 9;
            if (sq1 < 0 && sq1 > psq1 && psq1 < ppsq1)
                iJunk = 9;

            // 9/21 cross show
            if (nn > twone && prev_nn <= prev_twone)
                DrawText(bar, "X", Color.Yellow, Color.Transparent);
            if (nn < twone && prev_nn >= prev_twone)
                DrawText(bar, "X", Color.Yellow, Color.Transparent);

            if (eqHigh && candle.Close > 0)
                DrawText(bar - 1, "Equal\nHigh", Color.Lime, Color.Transparent, false, true);
            if (eqLow && candle.Close > 0)
                DrawText(bar - 1, "Equal\nLow", Color.Yellow, Color.Transparent, false, true);

            if (c0G && c1R && c2R && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta < 0 && candle.Close > 0)
                DrawText(bar, "Vol\nRev", Color.Yellow, Color.Transparent, false, true);
            if (c0R && c1G && c2G && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta > 0 && candle.Close > 0)
                DrawText(bar, "Vol\nRev", Color.Lime, Color.Transparent, false, true);

            // ========================================================================
            // ========================    UP CONDITIONS    ===========================
            // ========================================================================

            if ((candle.Delta < iMinDelta) || (!macdUp && bUseMACD) || (psarSell && bUsePSAR) || (!fisherUp && bUseFisher) || (value < t3 && bUseT3) || (value < kama9 && bUseKAMA) || (t1 < 0 && bUseWaddah) || (ao < 0 && bUseAO) || (st == 0 && bUseSuperTrend) || (sq1 < 0 && bUseSqueeze) || (x < iMinADX))
                bShowUp = false;

            if (green && bShowUp)
                iJunk = candle.Low - (_tick * 2);

            // ========================================================================
            // ========================    DOWN CONDITIONS    =========================
            // ========================================================================

            if ((candle.Delta > (iMinDelta * -1)) || (psarBuy && bUsePSAR) || (!macdDown && bUseMACD) || (!fisherDown && bUseFisher) || (value > kama9 && bUseKAMA) || (value > t3 && bUseT3) || (t1 >= 0 && bUseWaddah) || (ao > 0 && bUseAO) || (st == 0 && bUseSuperTrend) || (sq1 > 0 && bUseSqueeze) || (x < iMinADX))
                bShowDown = false;

            if (red && bShowDown)
                iJunk = candle.High + _tick * 2;

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

            // ========================================================================
            // =======================    REVERSAL PATTERNS    ========================
            // ========================================================================

            // Bollinger band bounce
                if (candle.High > bb_top && candle.Open < bb_top && c0R && candle.Close < p1C.Close && upWick50PerLarger)
                    DrawText(bar, "Wick", Color.Lime, Color.Transparent, false, true);
                if (candle.Low < bb_bottom && candle.Open > bb_bottom && c0G && candle.Close > p1C.Close && downWick50PerLarger)
                    DrawText(bar, "Wick", Color.Orange, Color.Transparent, false, true);

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

            // Intensity signal
            var candleSeconds = Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds);
            if (candleSeconds is 0) candleSeconds = 1;
            var volPerSecond = candle.Volume / candleSeconds;
            var deltaPer1 = candle.Delta > 0 ? (candle.Delta / candle.MaxDelta) : (candle.Delta / candle.MinDelta);
            var deltaIntense = Math.Abs((candle.Delta * deltaPer1) * (candle.Volume / candleSeconds));

            if (!bNewsProcessed)
                LoadStock(bar);

            if (false)
            {
                OpenPosition(OrderDirections.Buy);
            }

            if (false)
            {
                OpenPosition(OrderDirections.Sell);
            }
        }

        protected override void OnStopping()
        {
            if (CurrentPosition != 0 && bCloseOnStop)
            {
                RaiseShowNotification($"Closing current position {CurrentPosition} on stopping.");
                CloseCurrentPosition();
            }

            base.OnStopping();
        }

        #endregion

        #region Private methods

        // ========================================================================
        // =======================    PRIVATE METHODS   ===========================
        // ========================================================================

        private void OpenPosition(OrderDirections direction)
        {
            var order = new Order
            {
                Portfolio = Portfolio,
                Security = Security,
                Direction = direction,
                Type = OrderTypes.Market,
                QuantityToFill = GetOrderVolume(),
            };

            OpenOrder(order);
        }

        private void CloseCurrentPosition()
        {
            var order = new Order
            {
                Portfolio = Portfolio,
                Security = Security,
                Direction = CurrentPosition > 0 ? OrderDirections.Sell : OrderDirections.Buy,
                Type = OrderTypes.Market,
                QuantityToFill = Math.Abs(CurrentPosition),
            };

            OpenOrder(order);
        }

        private decimal GetOrderVolume()
        {
            if (CurrentPosition == 0)
                return Volume;

            if (CurrentPosition > 0)
                return Volume + CurrentPosition;

            return Volume + Math.Abs(CurrentPosition);
        }

        private decimal VolSec(IndicatorCandle c) { return c.Volume / Convert.ToDecimal((c.LastTime - c.Time).TotalSeconds); }

        protected void DrawText(int bBar, String strX, Color cI, Color cB, bool bOverride = false, bool bSwap = false)
        {
        }

            #endregion
        }
    }
