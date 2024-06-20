namespace FiduciaryCalculator
{
    public readonly struct BondPricingResults
    {
        /// <summary>
        ///     Calculation date.
        /// </summary>
        public DateTime PricingDate { get; init; }

        /// <summary>
        ///     Current security price.
        /// </summary>
        public double Price { get; init; }

        /// <summary>
        ///     Current security price adjustment.
        /// </summary>
        public double PriceAdjustment { get; init; }

        /// <summary>
        ///     Current security duration.
        /// </summary>
        public double Duration { get; init; }

        /// <summary>
        ///     Current security modified duration.
        /// </summary>
        public double ModifiedDuration { get; init; }

        /// <summary>
        ///     Current security DV01.
        /// </summary>
        public double DollarValue01 { get; init; }

        /// <summary>
        ///     Security yield to maturity / to offer
        /// </summary>
        public double Ytm { get; init; }

        /// <summary>
        ///     Current G-Spread.
        /// </summary>
        public double Gspread { get; init; }

        /// <summary>
        ///     Current Z-Spread.
        /// </summary>
        public double Zspread { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public Discounting Discounting { get; init; }

        /// <summary>
        ///     Gets value that indicates that any of pricing results ar bad.
        /// </summary>
        public bool HasBadResults => !double.IsNormal(Duration)
                                  || !double.IsNormal(Gspread)
                                  || !double.IsNormal(Zspread)
                                  || !double.IsNormal(Ytm);
    }





}
