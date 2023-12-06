using System;

namespace MyHome.Utils.Externals
{
    //https://github.com/porrey/Solar-Calculator
    /// <summary>
	/// Provides mathematical operations to calculate the sunrise and sunset for a given date.
	/// </summary>
	public class SolarTimes
    {
        private decimal _longitude = 0M;
        private decimal _latitude = 0M;

        #region Constructors
        /// <summary>
        /// Creates a default instance of the SolarTimes object.
        /// </summary>
        public SolarTimes()
        {
            this.ForDate = DateTime.Now;
        }

        /// <summary>
        /// Creates an instance of the SolarTimes object with the specified For Date.
        /// </summary>
        /// <param name="forDate">Specifies the Date for which the sunrise and sunset will be calculated.</param>
        public SolarTimes(DateTimeOffset forDate)
        {
            this.ForDate = forDate;
        }

        /// <summary>
        /// Creates an instance of the SolarTimes object with the specified For Date, Latitude and Longitude.
        /// </summary>
        /// <param name="forDate">Specifies the Date for which the sunrise and sunset will be calculated.</param>
        /// <param name="latitude">Specifies the angular measurement of north-south location on Earth's surface.</param>
        /// <param name="longitude">Specifies the angular measurement of east-west location on Earth's surface.</param>
        public SolarTimes(DateTimeOffset forDate, decimal latitude, decimal longitude)
        {
            this.ForDate = forDate;
            this.Latitude = latitude;
            this.Longitude = longitude;
        }

        public SolarTimes(DateTimeOffset forDate, double latitude, double longitude)
        {
            this.ForDate = forDate;
            this.Latitude = (decimal)latitude;
            this.Longitude = (decimal)longitude;
        }

        /// <summary>
        /// Creates an instance of the SolarTimes object with the specified For Date, Latitude Longitude and Time Zone Offset
        /// </summary>
        /// <param name="forDate">Specifies the Date for which the sunrise and sunset will be calculated.</param>
        /// <param name="timeZoneOffset">Specifies the time zone offset for the specified date in hours.</param>
        /// <param name="latitude">Specifies the angular measurement of north-south location on Earth's surface.</param>
        /// <param name="longitude">Specifies the angular measurement of east-west location on Earth's surface.</param>
        public SolarTimes(DateTime forDate, int timeZoneOffset, decimal latitude, decimal longitude)
        {
            this.ForDate = new DateTimeOffset(forDate, TimeSpan.FromHours(timeZoneOffset));
            this.Latitude = latitude;
            this.Longitude = longitude;
        }
        #endregion

        #region Public Members
        /// <summary>
        /// Specifies the Date for which the sunrise and sunset will be calculated.
        /// </summary>
        public DateTimeOffset ForDate { get; set; }

        /// <summary>
        /// Angular measurement of east-west location on Earth's surface. Longitude is defined from the 
        /// prime meridian, which passes through Greenwich, England. The international date line is defined 
        /// around +/- 180° longitude. (180° east longitude is the same as 180° west longitude, because 
        /// there are 360° in a circle.) Many astronomers define east longitude as positive. This 
        /// solar calculator conforms to the international standard, with east longitude positive.
        /// (Spreadsheet Column B, Row 4)
        /// </summary>
        public decimal Longitude
        {
            get => this._longitude;
            set
            {
                if (value is >= (-180M) and <= 180M)
                    this._longitude = value;
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The value for Longitude must be between -180° and 180°.");
                }
            }
        }

        /// <summary>
        /// Angular measurement of north-south location on Earth's surface. Latitude ranges from 90° 
        /// south (at the south pole; specified by a negative angle), through 0° (all along the equator), 
        /// to 90° north (at the north pole). Latitude is usually defined as a positive value in the 
        /// northern hemisphere and a negative value in the southern hemisphere.
        /// (Spreadsheet Column B, Row 3)
        /// </summary>
        public decimal Latitude
        {
            get => this._latitude;
            set
            {
                if (value is >= (-90M) and <= 90M)
                    this._latitude = value;
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The value for Latitude must be between -90° and 90°.");
                }
            }
        }

        /// <summary>
        /// Gets the time zone offset for the specified date.
        /// Time Zones are longitudinally defined regions on the Earth that keep a common time. A time 
        /// zone generally spans 15° of longitude, and is defined by its offset (in hours) from UTC. 
        /// For example, Mountain Standard Time (MST) in the US is 7 hours behind UTC (MST = UTC - 7).
        /// (Spreadsheet Column B, Row 5)
        /// </summary>
        public decimal TimeZoneOffset => (decimal)this.ForDate.Offset.TotalHours;

        /// <summary>
        /// Sun Rise Time  
        /// (Spreadsheet Column Y)
        /// </summary>
        public DateTime Sunrise
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays - (this.HourAngleSunrise * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// Sun Set Time
        /// (Spreadsheet Column Z)
        /// </summary>
        public DateTime Sunset
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays + (this.HourAngleSunrise * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// The Astronomical Dawn Time
        /// This is when the sun is &lt; 18 degrees below the horizon
        /// </summary>
        public DateTime DawnAstronomical
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays - (this.HourAngleDawnAstronomical * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// Astronomical Dusk Time
        /// This is when the sun is &lt; 18 degrees below the horizon
        /// </summary>
        public DateTime DuskAstronomical
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays + (this.HourAngleDawnAstronomical * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }
        /// <summary>
        /// The Civil Dawn Time
        /// This is when the sun is &lt; 6 degrees below the horizon
        /// </summary>
        public DateTime DawnCivil
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays - (this.HourAngleDawnCivil * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// Civil Dusk Time
        /// This is when the sun is &lt; 6 degrees below the horizon
        /// </summary>
        public DateTime DuskCivil
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays + (this.HourAngleDawnCivil * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// The Nautical Dawn Time
        /// This is when the sun is &lt; 12 degrees below the horizon
        /// </summary>
        public DateTime DawnNautical
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays - (this.HourAngleDawnNautical * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// Nautical Dusk Time
        /// This is when the sun is &lt; 12 degrees below the horizon
        /// </summary>
        public DateTime DuskNautical
        {
            get
            {
                var dayFraction = (decimal)this.SolarNoon.TimeOfDay.TotalDays + (this.HourAngleDawnNautical * 4M / 1440M);
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// As light from the sun (or another celestial body) travels from the vacuum of space into Earth's atmosphere, the 
        /// path of the light is bent due to refraction. This causes stars and planets near the horizon to appear higher in 
        /// the sky than they actually are, and explains how the sun can still be visible after it has physically passed 
        /// beyond the horizon at sunset. See also apparent sunrise.
        /// </summary>
        public decimal AtmosphericRefraction { get; set; } = 0.833M;
        #endregion

        #region Computational Members
        /// <summary>
        /// Time past local midnight.
        /// (Spreadsheet Column E)
        /// </summary>	
        public decimal TimePastLocalMidnight =>
                // ***
                // *** .1 / 24
                // ***
                (decimal)this.ForDate.TimeOfDay.TotalDays;

        /// <summary>
        /// Julian Day: a time period used in astronomical circles, defined as the number of days 
        /// since 1 January, 4713 BCE (Before Common Era), with the first day defined as Julian 
        /// day zero. The Julian day begins at noon UTC. Some scientists use the term Julian day 
        /// to mean the numerical day of the current year, where January 1 is defined as day 001. 
        /// (Spreadsheet Column F)
        /// </summary>
        public decimal JulianDay =>
                // ***
                // *** this.TimePastLocalMidnight was removed since the time is in ForDate
                // ***
                ToExcelDateValue(this.ForDate.Date) + 2415018.5M - (this.TimeZoneOffset / 24M);

        /// <summary>
        /// Julian Century
        /// calendar established by Julius Caesar in 46 BC, setting the number of days in a year at 365, 
        /// except for leap years which have 366, and occurred every 4 years. This calendar was reformed 
        /// by Pope Gregory XIII into the Gregorian calendar, which further refined leap years and corrected 
        /// for past errors by skipping 10 days in October of 1582. 
        /// (Spreadsheet Column G)
        /// </summary>
        public decimal JulianCentury => (this.JulianDay - 2451545M) / 36525M;

        /// <summary>
        /// Sun's Geometric Mean Longitude (degrees): Geometric Mean Ecliptic Longitude of Sun.
        /// (Spreadsheet Column I)
        /// </summary>
        public decimal SunGeometricMeanLongitude => Mod(280.46646M + (this.JulianCentury * (36000.76983M + (this.JulianCentury * 0.0003032M))), 360M);

        /// <summary>
        /// Sun's Mean Anomaly (degrees): Position of Sun relative to perigee
        /// (Spreadsheet Column J)
        /// </summary>
        public decimal SunMeanAnomaly => 357.52911M + (this.JulianCentury * (35999.05029M - (0.0001537M * this.JulianCentury)));

        /// <summary>
        /// Eccentricity of the Earth's Orbit: Eccentricity e is the ratio of half the distance between the foci c to
        /// the semi-major axis a: e = c / a. For example, an orbit with e = 0 is circular, e = 1 is parabolic, and e 
        /// between 0 and 1 is elliptic.
        /// (Spreadsheet Column K)
        /// </summary>
        public decimal EccentricityOfEarthOrbit => 0.016708634M - (this.JulianCentury * (0.000042037M + (0.0000001267M * this.JulianCentury)));

        /// <summary>
        /// Sun Equation of the Center: Difference between mean anomaly and true anomaly.
        /// (Spreadsheet Column L)
        /// </summary>
        public decimal SunEquationOfCenter => ((decimal)Math.Sin(ToRadians(this.SunMeanAnomaly)) * (1.914602M - (this.JulianCentury * (0.004817M + (0.000014M * this.JulianCentury))))) + ((decimal)Math.Sin(ToRadians(this.SunMeanAnomaly * 2)) * (0.019993M - (0.000101M * this.JulianCentury))) + ((decimal)Math.Sin(ToRadians(this.SunMeanAnomaly * 3)) * 0.000289M);

        /// <summary>
        /// Sun True Longitude (degrees)
        /// (Spreadsheet Column M)
        /// </summary>
        public decimal SunTrueLongitude => this.SunGeometricMeanLongitude + this.SunEquationOfCenter;

        /// <summary>
        /// Sun Apparent Longitude (degrees)
        /// (Spreadsheet Column P)
        /// </summary>
        public decimal SunApparentLongitude
        {
            get
            {
                var a1 = 125.04M - (1934.136M * this.JulianCentury);
                return this.SunTrueLongitude - 0.00569M - (0.00478M * (decimal)Math.Sin(ToRadians(a1)));
            }
        }

        /// <summary>
        /// Mean Ecliptic Obliquity (degrees): Inclination of ecliptic plane w.r.t. celestial equator
        /// (Spreadsheet Column Q)
        /// </summary>
        public decimal MeanEclipticObliquity =>
                // ***
                // *** Formula 22.3 from Page 147 of Astronomical Algorithms, Second Edition (Jean Meeus)
                // *** Original spreadsheet formula based on 22.2 same page of book
                // ***
                23M + ((26M + ((21.448M - (this.JulianCentury * (46.815M + (this.JulianCentury * (0.00059M - (this.JulianCentury * 0.001813M)))))) / 60M)) / 60M);

        /// <summary>
        /// Obliquity Correction (degrees)
        /// (Spreadsheet Column R)
        /// </summary>
        public decimal ObliquityCorrection
        {
            get
            {
                var a1 = 125.04M - (1934.136M * this.JulianCentury);
                return this.MeanEclipticObliquity + (0.00256M * (decimal)Math.Cos(ToRadians(a1)));
            }
        }

        /// <summary>
        /// Solar Declination (Degrees): The measure of how many degrees North (positive) or South (negative) 
        /// of the equator that the sun is when viewed from the center of the earth.  This varies from 
        /// approximately +23.5 (North) in June to -23.5 (South) in December.
        /// (Spreadsheet Column T)
        /// </summary>
        public decimal SolarDeclination
        {
            get
            {
                var radians = (decimal)Math.Asin((double)((decimal)Math.Sin(ToRadians(this.ObliquityCorrection)) * (decimal)Math.Sin(ToRadians(this.SunApparentLongitude))));
                return ToDegrees(radians);
            }
        }

        /// <summary>
        /// Var Y
        /// (Spreadsheet Column U)
        /// </summary>
        public decimal VarY => (decimal)Math.Tan(ToRadians(this.ObliquityCorrection / 2)) * (decimal)Math.Tan(ToRadians(this.ObliquityCorrection / 2));

        /// <summary>
        /// Equation of Time (minutes)
        /// Accounts for changes in the time of solar noon for a given location over the course of a year. Earth's 
        /// elliptical orbit and Kepler's law of equal areas in equal times are the culprits behind this phenomenon.
        /// (Spreadsheet Column V)
        /// </summary>
        public decimal EquationOfTime => 4M * ToDegrees((this.VarY * (decimal)Math.Sin(2 * ToRadians(this.SunGeometricMeanLongitude))) -
                    (2M * this.EccentricityOfEarthOrbit * (decimal)Math.Sin(ToRadians(this.SunMeanAnomaly))) +
                    (4M * this.EccentricityOfEarthOrbit * this.VarY * (decimal)Math.Sin(ToRadians(this.SunMeanAnomaly)) * (decimal)Math.Cos(2 * ToRadians(this.SunGeometricMeanLongitude))) -
                    (0.5M * this.VarY * this.VarY * (decimal)Math.Sin(4 * ToRadians(this.SunGeometricMeanLongitude))) -
                    (1.25M * this.EccentricityOfEarthOrbit * this.EccentricityOfEarthOrbit * (decimal)Math.Sin(ToRadians(this.SunMeanAnomaly * 2))));

        /// <summary>
        /// HA Sunrise (degrees)
        /// (Spreadsheet Column W)
        /// </summary>
        public decimal HourAngleSunrise
        {
            get
            {
                var a1 = 90M + this.AtmosphericRefraction;
                var radians = (decimal)Math.Acos((Math.Cos(ToRadians(a1)) / (Math.Cos(ToRadians(this.Latitude)) * Math.Cos(ToRadians(this.SolarDeclination)))) -
                    (Math.Tan(ToRadians(this.Latitude)) * Math.Tan(ToRadians(this.SolarDeclination))));
                return ToDegrees(radians);
            }
        }

        /// <summary>
        /// HA Astronomical Dawn (degrees)
        /// </summary>
        public decimal HourAngleDawnAstronomical
        {
            get
            {
                var a1 = 108M;// + this.AtmosphericRefraction; // Online calculators I could test against don't include refraction
                var radians = (decimal)Math.Acos((Math.Cos(ToRadians(a1)) / (Math.Cos(ToRadians(this.Latitude)) * Math.Cos(ToRadians(this.SolarDeclination)))) -
                    (Math.Tan(ToRadians(this.Latitude)) * Math.Tan(ToRadians(this.SolarDeclination))));
                return ToDegrees(radians);
            }
        }

        /// <summary>
        /// HA Civil Dawn (degrees)
        /// </summary>
        public decimal HourAngleDawnCivil
        {
            get
            {
                var a1 = 96M;// + this.AtmosphericRefraction; // Online calculators I could test against don't include refraction
                var radians = (decimal)Math.Acos((Math.Cos(ToRadians(a1)) / (Math.Cos(ToRadians(this.Latitude)) * Math.Cos(ToRadians(this.SolarDeclination)))) -
                    (Math.Tan(ToRadians(this.Latitude)) * Math.Tan(ToRadians(this.SolarDeclination))));
                return ToDegrees(radians);
            }
        }

        /// <summary>
        /// HA Nautical Dawn (degrees)
        /// (Spreadsheet Column W)
        /// </summary>
        public decimal HourAngleDawnNautical
        {
            get
            {
                var a1 = 102M;// + this.AtmosphericRefraction; // Online calculators I could test against don't include refraction
                var radians = (decimal)Math.Acos((Math.Cos(ToRadians(a1)) / (Math.Cos(ToRadians(this.Latitude)) * Math.Cos(ToRadians(this.SolarDeclination)))) -
                    (Math.Tan(ToRadians(this.Latitude)) * Math.Tan(ToRadians(this.SolarDeclination))));
                return ToDegrees(radians);
            }
        }

        /// <summary>
        /// Solar Noon LST
        /// Defined for a given day for a specific longitude, it is the time when the sun crosses the meridian of 
        /// the observer's location. At solar noon, a shadow cast by a vertical pole will point either directly 
        /// north or directly south, depending on the observer's latitude and the time of year. 
        /// (Spreadsheet Column X)
        /// </summary>
        public DateTime SolarNoon
        {
            get
            {
                var dayFraction = (720M - (4M * this.Longitude) - this.EquationOfTime + (this.TimeZoneOffset * 60M)) / 1440M;
                return this.ForDate.Date.Add(TimeSpan.FromDays((double)dayFraction));
            }
        }

        /// <summary>
        /// Sunlight Duration: The amount of time the sun is visible during the specified day.
        /// (Spreadsheet Column AA)
        /// </summary>
        public TimeSpan SunlightDuration => TimeSpan.FromMinutes((double)(8M * this.HourAngleSunrise));

        /// <summary>
        /// Hour Angle (degrees)
        /// (Spreadsheet Column AC)
        /// </summary>
        public decimal HourAngleDegrees
        {
            get
            {
                var temp = this.TrueSolarTime / 4;
                return temp < 0 ? temp + 180 : temp - 180;
            }
        }

        /// <summary>
        /// Solar Zenith (degrees)
        /// (Spreadsheet Column AD)
        /// </summary>
        public decimal SolarZenith => ToDegrees
                (
                    (decimal)Math.Acos((Math.Sin(ToRadians(this.Latitude)) * Math.Sin(ToRadians(this.SolarDeclination))) +
                    (Math.Cos(ToRadians(this.Latitude)) * Math.Cos(ToRadians(this.SolarDeclination)) * Math.Cos(ToRadians(this.HourAngleDegrees))))
                );

        /// <summary>
        /// Solar Elevation (degrees)
        /// (Spreadsheet Column AE)
        /// </summary>
        public decimal SolarElevation => 90M - this.SolarZenith;

        /// <summary>
        /// Solar Azimuth (degrees)
        /// (Spreadsheet Column AH)
        /// </summary>
        public decimal SolarAzimuth
        {
            get
            {
                var angle = ToDegrees((decimal)Math.Acos(
                                              ((Math.Sin(ToRadians(this.Latitude)) * Math.Cos(ToRadians(this.SolarZenith))) -
                                                Math.Sin(ToRadians(this.SolarDeclination))) /
                                               (Math.Cos(ToRadians(this.Latitude)) * Math.Sin(ToRadians(this.SolarZenith)))));

                if (this.HourAngleDegrees > 0.0M)
                {
                    angle += 180M;
                    return angle - ((decimal)Math.Floor((double)(angle / 360.0M)) * 360.0M);
                }
                else
                {
                    angle = 540M - angle;
                    return angle - ((decimal)Math.Floor((double)(angle / 360.0M)) * 360.0M);
                }
            }
        }
        #endregion

        #region Additional Members
        /// <summary>
        /// Gets the True Solar Time in minutes. The Solar Time is defined as the time elapsed since the most recent 
        /// meridian passage of the sun. This system is based on the rotation of the Earth with respect to the sun. 
        /// A mean solar day is defined as the time between one solar noon and the next, averaged over the year.
        /// (Spreadsheet Column AB)
        /// </summary>
        public decimal TrueSolarTime => Mod((this.TimePastLocalMidnight * 1440M) + this.EquationOfTime + (4M * this.Longitude) - (60M * this.TimeZoneOffset), 1440M);
        #endregion

        public static decimal ToExcelDateValue(DateTime value)
        {
            if (value.Date <= new DateTime(1900, 1, 1))
            {
                var d = (decimal)value.Subtract(new DateTime(1899, 12, 30).Date).TotalDays;
                var c = Math.Floor(d);
                return 1M + (d - c);
            }
            else
            {
                return (decimal)value.Subtract(new DateTime(1899, 12, 30).Date).TotalDays;
            }
        }
        public static decimal Mod(decimal number, decimal divisor)
        {
            if (divisor != 0M)
                return number - (divisor * Math.Floor(number / divisor));
            else
            {
                throw new DivideByZeroException("The value for divisor cannot be zero.");
            }
        }
        public static double ToRadians(decimal degrees)
        {
            // ***
            // *** Factor = pi / 180 
            // ***
            return (double)(degrees * (decimal)Math.PI / 180M);
        }
        public static decimal ToDegrees(decimal radians)
        {
            // ***
            // *** Factor = 180 / pi 
            // ***
            return radians * (180M / (decimal)Math.PI);
        }
    }
}
