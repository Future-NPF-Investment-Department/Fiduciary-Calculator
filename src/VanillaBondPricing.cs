using RuDataAPI.Extensions;

namespace FiduciaryCalculator
{
    public class VanillaBondPricing
    {
#pragma warning disable IDE1006 // Naming Styles
        private const byte PRCE = 1;
        private const byte CPNR = PRCE << 1;
        private const byte DISC = CPNR << 1;
#pragma warning restore IDE1006 // Naming Styles

        private readonly Discounting _disc;
        private readonly byte _flags;

        private double? _price;
        private double? _yield;
        private double? _zsprd;
        private double? _cpnrate;

        private double _priceAdjustment;
        private BondPricingResults _results;

        public VanillaBondPricing(Discounting discounting, double? price, double? ytm, double? zspread, double? couponRate)
        {
            _price = price;
            _yield = ytm;
            _zsprd = zspread;
            _cpnrate = couponRate;
            _disc = discounting;
            _flags = DefinePricingFlags();
        }


        public BondPricingResults Results => _results;


        public VanillaBondPricing Price()
        {
            switch (_flags)
            {
                case PRCE | DISC | CPNR:
                    var price = CalcPrice();
                    _priceAdjustment = price - _price!.Value;
                    _price = price;
                    _yield ??= CalcYtmVsPrice(_price!.Value);
                    _zsprd ??= CalcZspreadVsPrice(_price!.Value);
                    break;

                case PRCE | CPNR:
                    _yield = CalcYtmVsPrice(_price!.Value);
                    _zsprd = CalcZspreadVsPrice(_price!.Value);
                    break;

                case PRCE | DISC:
                    _cpnrate = CalcCouponRateVsPrice(_price!.Value);
                    _yield ??= CalcYtmVsPrice(_price!.Value);
                    _zsprd ??= CalcZspreadVsPrice(_price!.Value);
                    break;

                case DISC | CPNR:
                    _price = CalcPrice();
                    _yield ??= CalcYtmVsPrice(_price!.Value);
                    _zsprd ??= CalcZspreadVsPrice(_price!.Value);
                    break;

                case PRCE:
                    goto default;

                case DISC:
                    goto default;

                case CPNR:
                    goto default;

                default:
                    throw new PricingException("Not enough data for pricing.");
            }

            double macd = _disc.Select(d => d.DiscountedValue * d.TimeToFlowDate).Sum() / _disc.Sum(d => d.DiscountedValue);
            double modd = macd / (1 + _yield.Value);
            double dv01 = modd * _price.Value * 0.0001;
            double gsprd = (_yield!.Value - _disc.Curve.GetValueForTenor(macd)) * 10_000;


            _results = new BondPricingResults()
            {
                PricingDate = _disc.Curve.Date,
                Price = _price!.Value,
                PriceAdjustment = _priceAdjustment,
                Duration = macd,
                ModifiedDuration = modd,
                DollarValue01 = dv01,
                Ytm = _yield ?? .0,
                Gspread = gsprd,
                Zspread = _zsprd ?? .0,
                Discounting = _disc
            };

            return this;
        }


        public double CalcPriceVsYtm(double ytm)
        {
            UpdateDiscounting(ytm, .0, _cpnrate);
            return _disc.Sum(d => d.DiscountedValue);
        }

        public double CalcPriceVsZspread(double zspread)
        {
            UpdateDiscounting(null, zspread, _cpnrate);
            return _disc.Sum(d => d.DiscountedValue);
        }

        public double CalcPriceVsCpnRate(double couponRate)
        {
            UpdateDiscounting(_yield, _zsprd, couponRate);
            return _disc.Sum(d => d.DiscountedValue);
        }

        public double CalcYtmVsPrice(double price)
        {
            return BondPricing.Solve(func, -0.25, 0.25);

            // bond pricing function
            double func(double ytm) => CalcPriceVsYtm(ytm) - price;
        }

        public double CalcZspreadVsPrice(double price)
        {
            return BondPricing.Solve(func, -400, 500);

            // bond pricing function
            double func(double zsprd) => CalcPriceVsZspread(zsprd) - price;
        }

        public double CalcCouponRateVsPrice(double price)
        {
            return BondPricing.Solve(func, -0.25, 0.25);

            // bond pricing function
            double func(double cpn) => CalcPriceVsCpnRate(cpn) - price;
        }

        private void UpdateDiscounting(double? ytm, double? zsprd, double? cpnr)
        {
            foreach (var d in _disc)
            {
                double t = (d.Date - _disc.Curve.Date).Days / 365.0;
                double z = zsprd == null ? d.Zspread : zsprd.Value;
                double c = cpnr == null ? d.InterestRate : cpnr.Value;
                double r = ytm == null ? _disc.Curve.GetValueForTenor(t) : ytm.Value;
                double df = 1 / Math.Pow(1 + r + z / 10_000, t);

                d.InterestRate = c;
                d.InterestValue = d.FaceValue * c / 365.0 * d.Tenor.Days;
                d.TotalValue = d.InterestValue + d.AmortValue;
                d.TimeToFlowDate = t;
                d.DiscountRate = r;
                d.Zspread = z;
                d.DiscountFactor = df;
                d.DiscountedValue = d.TotalValue * df;
            }
        }

        private double CalcPrice()
        {
            return _yield != null
                ? CalcPriceVsYtm(_yield.Value)
                : CalcPriceVsZspread(_zsprd!.Value);
        }

        private byte DefinePricingFlags()
        {
            byte flags = 0;
            if (_price != null)
                flags |= 1;

            if (_cpnrate != null || _disc.Fetched)
                flags |= 2;

            if (_yield != null || _zsprd != null)
                flags |= 4;

            return flags;
        }

        private enum PricingFlags : byte
        {
            None = 0,
            HasPrice = 1 << 0,
            HasDiscountRate = 1 << 1,
            HasCouponRate = 1 << 2
        }
    }
}
