
namespace FiduciaryCalculator
{
    /// <summary>
    ///     Represents set of pricing metrics for particular security.
    /// </summary>
    public readonly struct SecurityPricing
    {
        /// <summary>
        ///     Security fair price.
        /// </summary>
        public double Price { get; init; }

        /// <summary>
        ///     Security duration.
        /// </summary>
        public double Duration { get; init; }

        /// <summary>
        ///     Security yield to maturity / to offer
        /// </summary>
        public double Ytm { get; init; }

        /// <summary>
        ///     G-Spread.
        /// </summary>
        public double Gspread { get; init; }        

        /// <summary>
        ///     Z-Spread
        /// </summary>
        public double Zspread { get; init; }
        
        /// <summary>
        ///     Trading volume for the past 10 business days.
        /// </summary>
        public double TradeVolume { get; init; }

    }
}
