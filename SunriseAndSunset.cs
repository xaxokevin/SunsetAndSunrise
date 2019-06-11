using System;
using System.Diagnostics;

namespace SolarInfo {
    public class SolarInfo {
        public double SolarDeclination { get; private set; }
        public TimeSpan EquationOfTime { get; private set; }
        public DateTime Sunrise { get; private set; }
        public DateTime Sunset { get; private set; }
        public TimeSpan? Noon { get; private set; }
        public DateTime Date { get; private set; }

        private SolarInfo () { }

        static void Main (string[] args) {
            DateTime datevalue = DateTime.Now;
            DateTime dateC = new DateTime (2019, 04, 29);
            SolarInfo myInfo = ForDate (38.897079, -77.036605, datevalue, 0);
            Console.WriteLine ("Fecha => " + myInfo.Date);
            Console.WriteLine ("Declinación solar => " + myInfo.SolarDeclination);
            Console.WriteLine ("Ecuación del tiempo => " + myInfo.EquationOfTime);
            Console.WriteLine ("Orto => " + myInfo.Sunrise);
            Console.WriteLine ("Ocaso => " + myInfo.Sunset);
            Console.WriteLine ("Medio día solar => " + myInfo.Noon);

        }

        /// <summary>
        /// Devuelve la información solar del día especificado, para una latitud y una langitud concreta. 
        /// </summary>
        /// <param name="latitude">Latitud de la ubicación a calcular su info</param>
        /// <param name="longitude">Latitud de la ubicación a calcular su info</param>
        /// <param name="date">Día del que se necisita la información</param>
        /// <param name="UTC_GMT">Desfase horario teniendo en cuenta los horarios de verano e invierno</param>
        /// <returns></returns>
        public static SolarInfo ForDate (double latitude, double longitude, DateTime date, double UTC_GMT) {
            var info = new SolarInfo ();
            info.Date = date = date.Date;

            var year = date.Year;
            var month = date.Month;
            var day = date.Day;

            if (latitude >= -90 && latitude < -89) {
                //Todas las latitudes entre -89 y -90 se establecerán en -89
                latitude = -89;
            }

            if (latitude <= 90 && latitude > 89) {
                //Todas las latitudes entre 89 y 90 se establecerán en 89
                latitude = 89;
            }
            if (latitude < 0) {
                UTC_GMT = -UTC_GMT;
            }

            //Calcula la hora del amanecer          
            var JD = calcJD (year, month, day);
            var doy = calcDayOfYear (month, day, isLeapYear (year));
            var T = calcTimeJulianCent (JD);

            var solarDec = calcSunDeclination (T);
            var eqTime = calcEquationOfTime (T); // (en minutos)

            // Calcula el amanecer para esta fecha
            // si no se encuentra ningún amanecer, establece la bandera nosunrise
            var nosunrise = false;

            var riseTimeGMT = calcSunriseUTC (JD, latitude, longitude, UTC_GMT);
            nosunrise = !isNumber (riseTimeGMT);

            // Calcula la puesta de sol para esta fecha
            // si no se encuentra ninguna puesta de sol, establece la bandera nosunset
            var nosunset = false;
            var setTimeGMT = calcSunsetUTC (JD, latitude, longitude, UTC_GMT);
            if (!isNumber (setTimeGMT)) {
                nosunset = true;
            }

            if (!nosunrise) // Se encontró el amanecer
            {
                info.Sunrise = date.Date.AddMinutes (riseTimeGMT);
            }

            if (!nosunset) // Se encontró la puesta de sol
            {
                info.Sunset = date.Date.AddMinutes (setTimeGMT);
            }

            // Calcula el mediodía solar para esta fecha.
            var solNoonGMT = calcSolNoonUTC (T, longitude, UTC_GMT);

            if (!(nosunset || nosunrise)) {
                info.Noon = TimeSpan.FromMinutes (solNoonGMT);
            }

            var tsnoon = calcTimeJulianCent (calcJDFromJulianCent (T) - 0.5 + solNoonGMT / 1440.0);
            eqTime = calcEquationOfTime (tsnoon);
            solarDec = calcSunDeclination (tsnoon);

            info.EquationOfTime = TimeSpan.FromMinutes (eqTime);
            info.SolarDeclination = solarDec;

            // Reporta casos especiales de no amanecer.
            if (nosunrise) {
                // Si es hemisferio norte y primavera o verano, o
                // Si es hemisferio sur y otoño o invierno, usa
                // amanecer anterior y siguiente atardecer

                if (latitude > 66.4 && doy > 79 && doy < 267 ||
                    latitude < -66.4 && (doy < 83 || doy > 263)) {
                    var newjd = findRecentSunrise (JD, latitude, longitude, UTC_GMT);
                    var newtime = calcSunriseUTC (newjd, latitude, longitude, UTC_GMT);

                    if (newtime > 1440) {
                        newtime -= 1440;
                        newjd += 1.0;
                    }
                    if (newtime < 0) {
                        newtime += 1440;
                        newjd -= 1.0;
                    }

                    info.Sunrise = ConvertToDate (newtime, newjd);
                }

                // Si es hemisferio norte y otoño o invierno, o
                // Si es hemisferio sur y primavera o verano, usa
                // el próximo amanecer y el atardecer anterior
                else if (latitude > 66.4 && (doy < 83 || doy > 263) ||
                    latitude < -66.4 && doy > 79 && doy < 267) {
                    var newjd = findNextSunrise (JD, latitude, longitude, UTC_GMT);
                    var newtime = calcSunriseUTC (newjd, latitude, longitude, UTC_GMT);

                    if (newtime > 1440) {
                        newtime -= 1440;
                        newjd += 1.0;
                    }
                    if (newtime < 0) {
                        newtime += 1440;
                        newjd -= 1.0;
                    }

                    info.Sunrise = ConvertToDate (newtime, newjd);
                } else {
                    Debug.Fail ("No se puede encontrar el amanecer!");
                }

            }

            if (nosunset) {
                // Si es hemisferio norte y primavera o verano, o
                // Si es hemisferio sur y otoño o invierno, use
                // amanecer anterior y siguiente atardecer

                if (latitude > 66.4 && doy > 79 && doy < 267 ||
                    latitude < -66.4 && (doy < 83 || doy > 263)) {
                    var newjd = findNextSunset (JD, latitude, longitude, UTC_GMT);
                    var newtime = calcSunsetUTC (newjd, latitude, longitude, UTC_GMT);

                    if (newtime > 1440) {
                        newtime -= 1440;
                        newjd += 1.0;
                    }
                    if (newtime < 0) {
                        newtime += 1440;
                        newjd -= 1.0;
                    }

                    info.Sunset = ConvertToDate (newtime, newjd);
                }

                // Si es hemisferio norte y otoño o invierno, o
                // Si es hemisferio sur y primavera o verano, use
                // el próximo amanecer y el último atardecer
                else if (latitude > 66.4 && (doy < 83 || doy > 263) ||
                    latitude < -66.4 && doy > 79 && doy < 267) {
                    var newjd = findRecentSunset (JD, latitude, longitude, UTC_GMT);
                    var newtime = calcSunsetUTC (newjd, latitude, longitude, UTC_GMT);

                    if (newtime > 1440) {
                        newtime -= 1440;
                        newjd += 1.0;
                    }
                    if (newtime < 0) {
                        newtime += 1440;
                        newjd -= 1.0;
                    }

                    info.Sunset = ConvertToDate (newtime, newjd);
                } else {
                    Debug.Fail ("No se puede encontrar la puesta del sol!");
                }

            }

            return info;
        }

        // Esto está inspirado en timeStringShortAMPM
        /// <summary>
        /// Esto trata los días julianos fraccionarios como días enteros, por lo que la porción de minutos del día juliano se reemplazará con el valor del parámetro de minutos
        /// </summary>
        /// <param name="minutes">minutos</param>
        /// <param name="JD">Dias julianos</param>
        /// <returns>calcDayFromJD</returns>
        private static DateTime ConvertToDate (double minutes, double JD) {
            var julianday = JD;
            var floatHour = minutes / 60.0;
            var hour = Math.Floor (floatHour);
            var floatMinute = 60.0 * (floatHour - Math.Floor (floatHour));
            var minute = Math.Floor (floatMinute);
            var floatSec = 60.0 * (floatMinute - Math.Floor (floatMinute));
            var second = Math.Floor (floatSec + 0.5);

            minute += second >= 30 ? 1 : 0;

            if (minute >= 60) {
                minute -= 60;
                hour++;
            }

            if (hour > 23) {
                hour -= 24;
                julianday += 1.0;
            }

            if (hour < 0) {
                hour += 24;
                julianday -= 1.0;
            }

            return calcDayFromJD (julianday).Add (new TimeSpan (0, (int) hour, (int) minute, (int) second));
        }

        /// <summary>
        /// Calcula el dia a partir del dia juliano recibido
        /// </summary>
        /// <param name="jd"></param>
        /// <returns>DateTime</returns>
        private static DateTime calcDayFromJD (double jd) {
            var z = Math.Floor (jd + 0.5);
            var f = jd + 0.5 - z;

            double A = 0;
            if (z < 2299161) {
                A = z;
            } else {
                var alpha = Math.Floor ((z - 1867216.25) / 36524.25);
                A = z + 1 + alpha - Math.Floor (alpha / 4);
            }

            var B = A + 1524;
            var C = Math.Floor ((B - 122.1) / 365.25);
            var D = Math.Floor (365.25 * C);
            var E = Math.Floor ((B - D) / 30.6001);

            var day = B - D - Math.Floor (30.6001 * E) + f;
            var month = E < 14 ? E - 1 : E - 13;
            var year = month > 2 ? C - 4716 : C - 4715;

            return new DateTime ((int) year, (int) month, (int) day, 0, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// Comprueba si es año bisiesto
        /// </summary>
        /// <param name="yr">año</param>
        /// <returns></returns>
        private static bool isLeapYear (int yr) {
            return yr % 4 == 0 && yr % 100 != 0 || yr % 400 == 0;
        }

        /// <summary>
        /// Devuelve false si el valor no es un entero positivo, true si devuelve lo contrario. El código es de Javascript de Danny Goodman's Javascript Handbook, p. 372.
        /// </summary>
        /// <param name="inputVal"></param>
        /// <returns>inputVal</returns>
        private static bool isPosInteger (int inputVal) {
            return inputVal > 0;
        }

        /// <summary>
        /// Comprueba si el numero recibido es un número
        /// </summary>
        /// <param name="inputVal"></param>
        /// <returns></returns>
        private static bool isNumber (double inputVal) {
            return !double.IsNaN (inputVal);
        }

        /// <summary>
        /// Convierte de radianes a grados
        /// </summary>
        /// <param name="angleRad"></param>
        /// <returns>double en grados</returns>
        private static double radToDeg (double angleRad) {
            return 180.0 * angleRad / Math.PI;
        }

        /// <summary>
        /// Convierte de grados a radianes
        /// </summary>
        /// <param name="angleDeg"></param>
        /// <returns>double en radianes</returns>
        private static double degToRad (double angleDeg) {
            return Math.PI * angleDeg / 180.0;
        }

        /// <summary>
        /// Encuentra el día numérico del año a partir de la información de mes, día y si es bisiesto o no
        /// </summary>
        /// <param name="mn">mes</param>
        /// <param name="dy">dia</param>
        /// <param name="lpyr">boolean del año bisiesto</param>
        /// <returns>El día numérico del año.   </returns>
        private static int calcDayOfYear (int mn, int dy, bool lpyr) {
            var k = lpyr ? 1 : 2;
            var doy = Math.Floor (275d * mn / 9d) - k * Math.Floor ((mn + 9d) / 12d) + dy - 30;
            return (int) doy;
        }

        /// <summary>
        /// Deriva el dia de la semana del día juliano.
        /// </summary>
        /// <param name="juld">dia juliano</param>
        /// <returns>String con el dia de la semana</returns>
        private static string calcDayOfWeek (double juld) {
            var A = (juld + 1.5) % 7;
            var DOW = A == 0 ? "Sunday" : A == 1 ? "Monday" : A == 2 ? "Tuesday" : A == 3 ? "Wednesday" : A == 4 ? "Thursday" : A == 5 ? "Friday" : "Saturday";
            return DOW;
        }

        /// <summary>
        /// Calcula el dia juliano desde el día dado
        /// </summary>
        /// <param name="year">año</param>
        /// <param name="month">mes</param>
        /// <param name="day">dia</param>
        /// <returns>El día juliano correspondiente a la fecha.
        /// El número se devuelve para el comienzo del día. Los días fraccionarios deberían ser añadido más tarde.
        /// </returns>
        private static double calcJD (int year, int month, int day) {
            if (month <= 2) {
                year -= 1;
                month += 12;
            }

            var A = Math.Floor (year / 100d);
            var B = 2 - A + Math.Floor (A / 4d);

            var JD = Math.Floor (365.25 * (year + 4716)) + Math.Floor (30.6001 * (month + 1.0)) + day + B - 1524.5;

            return JD;
        }

        /// <summary>
        /// Convierte el día juliano en siglos desde J2000.0.
        /// </summary>
        /// <param name="jd">dia juliano a convertir</param>
        /// <returns>T: valor correspondiente al dia juliano</returns>
        private static double calcTimeJulianCent (double jd) {
            var T = (jd - 2451545.0) / 36525.0;
            return T;
        }

        /// <summary>
        /// Convierte siglos desde J2000.0 hasta el día juliano
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>JD: El día juliano correspondiente al valor dado</returns>
        private static double calcJDFromJulianCent (double t) {
            var JD = t * 36525.0 + 2451545.0;
            return JD;
        }

        /// <summary>
        /// Calcular la longitud media geométrica del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>longitud media del sol en grados</returns>
        private static double calcGeomMeanLongSun (double t) {
            var L0 = 280.46646 + t * (36000.76983 + 0.0003032 * t);
            while (L0 > 360.0) {
                L0 -= 360.0;
            }
            while (L0 < 0.0) {
                L0 += 360.0;
            }
            return L0; // en grados
        }

        /// <summary>
        /// Calcular la anomalía media geométrica del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>anomalía media del sol en grados</returns>
        private static double calcGeomMeanAnomalySun (double t) {
            var M = 357.52911 + t * (35999.05029 - 0.0001537 * t);
            return M; // en grados
        }

        /// <summary>
        /// Calcula la excentricidad de la orbita terrestre
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>la excentricidad sin unidad</returns>
        private static double calcEccentricityEarthOrbit (double t) {
            var e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
            return e; // sin unidad
        }

        /// <summary>
        /// Calcula la ecuación de centro para el sol.
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>valor en grados</returns>
        private static double calcSunEqOfCenter (double t) {
            var m = calcGeomMeanAnomalySun (t);

            var mrad = degToRad (m);
            var sinm = Math.Sin (mrad);
            var sin2m = Math.Sin (mrad + mrad);
            var sin3m = Math.Sin (mrad + mrad + mrad);

            var C = sinm * (1.914602 - t * (0.004817 + 0.000014 * t)) + sin2m * (0.019993 - 0.000101 * t) + sin3m * 0.000289;
            return C; // en grados
        }

        /// <summary>
        /// Calcula la verdadera longitud del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>la verdadera longitud del sol en grados</returns>
        private static double calcSunTrueLong (double t) {
            var l0 = calcGeomMeanLongSun (t);
            var c = calcSunEqOfCenter (t);

            var O = l0 + c;
            return O; // en grados
        }

        /// <summary>
        /// Calcula la anomalía verdadera del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>anomalía verdadera del sol en grados</returns>
        private static double calcSunTrueAnomaly (double t) {
            var m = calcGeomMeanAnomalySun (t);
            var c = calcSunEqOfCenter (t);

            var v = m + c;
            return v; // en grados
        }

        /// <summary>
        /// Calcula la distancia al son en unidad astronómica AU
        /// </summary>
        /// <param name="t">anomalía verdadera del sol en grados</param>
        /// <returns>radio del sol vector en AU</returns>
        private static double calcSunRadVector (double t) {
            var v = calcSunTrueAnomaly (t);
            var e = calcEccentricityEarthOrbit (t);

            var R = 1.000001018 * (1 - e * e) / (1 + e * Math.Cos (degToRad (v)));
            return R; // en AU
        }

        /// <summary>
        /// Calcula la longitud aparente del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>longitud aparente en grados</returns>
        private static double calcSunApparentLong (double t) {
            var o = calcSunTrueLong (t);

            var omega = 125.04 - 1934.136 * t;
            var lambda = o - 0.00569 - 0.00478 * Math.Sin (degToRad (omega));
            return lambda; // en grados
        }

        /// <summary>
        /// Calcula la oblicuidad media de la eclíptica
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>la oblicuidad media en grados</returns>
        private static double calcMeanObliquityOfEcliptic (double t) {
            var seconds = 21.448 - t * (46.8150 + t * (0.00059 - t * 0.001813));
            var e0 = 23.0 + (26.0 + seconds / 60.0) / 60.0;
            return e0; // en grados
        }

        /// <summary>
        /// Calcula la oblicuidad corregida de la eclíptica.
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>correción oblicua en grados</returns>
        private static double calcObliquityCorrection (double t) {
            var e0 = calcMeanObliquityOfEcliptic (t);

            var omega = 125.04 - 1934.136 * t;
            var e = e0 + 0.00256 * Math.Cos (degToRad (omega));
            return e; // en grados
        }

        /// <summary>
        /// Calcula la ascensión recta del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>ascensión recta del sol en grados</returns>
        private static double calcSunRtAscension (double t) {
            var e = calcObliquityCorrection (t);
            var lambda = calcSunApparentLong (t);

            var tananum = Math.Cos (degToRad (e)) * Math.Sin (degToRad (lambda));
            var tanadenom = Math.Cos (degToRad (lambda));
            var alpha = radToDeg (Math.Atan2 (tananum, tanadenom));
            return alpha; // en grados
        }

        /// <summary>
        /// Calcula la declinación del sol
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>declinación solar en grados</returns>
        private static double calcSunDeclination (double t) {
            var e = calcObliquityCorrection (t);
            var lambda = calcSunApparentLong (t);

            var sint = Math.Sin (degToRad (e)) * Math.Sin (degToRad (lambda));
            var theta = radToDeg (Math.Asin (sint));
            return theta; // en grados
        }

        /// <summary>
        /// Calcula la diferencia entre el tiempo solar real y el tiempo solar medio.
        /// </summary>
        /// <param name="t">numero de siglos julianos desde J2000.0</param>
        /// <returns>ecuación del tiempo en minutos de tiempo</returns>
        private static double calcEquationOfTime (double t) {
            var epsilon = calcObliquityCorrection (t);
            var l0 = calcGeomMeanLongSun (t);
            var e = calcEccentricityEarthOrbit (t);
            var m = calcGeomMeanAnomalySun (t);

            var y = Math.Tan (degToRad (epsilon) / 2.0);
            y *= y;

            var sin2l0 = Math.Sin (2.0 * degToRad (l0));
            var sinm = Math.Sin (degToRad (m));
            var cos2l0 = Math.Cos (2.0 * degToRad (l0));
            var sin4l0 = Math.Sin (4.0 * degToRad (l0));
            var sin2m = Math.Sin (2.0 * degToRad (m));

            var Etime = y * sin2l0 - 2.0 * e * sinm + 4.0 * e * y * sinm * cos2l0 -
                0.5 * y * y * sin4l0 - 1.25 * e * e * sin2m;

            return radToDeg (Etime) * 4.0; // en minutos de tiempo
        }

        /// <summary>
        /// Calcula el ángulo de la hora del sol al amanecer para la latitud
        /// </summary>
        /// <param name="lat">latitud del observador en grados</param>
        /// <param name="solarDec">ángulo de declinación del sol en grados</param>
        /// <returns>ángulo de la hora del amanecer en radianes</returns>
        private static double calcHourAngleSunrise (double lat, double solarDec) {
            var latRad = degToRad (lat);
            var sdRad = degToRad (solarDec);

            var HAarg = Math.Cos (degToRad (90.833)) / (Math.Cos (latRad) * Math.Cos (sdRad)) - Math.Tan (latRad) * Math.Tan (sdRad);
            var HA = Math.Acos (Math.Cos (degToRad (90.833)) / (Math.Cos (latRad) * Math.Cos (sdRad)) - Math.Tan (latRad) * Math.Tan (sdRad));
            return HA; // en radianes
        }

        /// <summary>
        /// Calcula el ángulo de la hora del sol al atardecer para la latitud
        /// </summary>
        /// <param name="lat">Latitud del observador en grados</param>
        /// <param name="solarDec">Ángulo de declinación del sol en grados</param>
        /// <returns>Ángulo de la hora de la puesta de sol en radianes</returns>
        private static double calcHourAngleSunset (double lat, double solarDec) {
            var latRad = degToRad (lat);
            var sdRad = degToRad (solarDec);

            var HAarg = Math.Cos (degToRad (90.833)) / (Math.Cos (latRad) * Math.Cos (sdRad)) - Math.Tan (latRad) * Math.Tan (sdRad);

            var HA = Math.Acos (Math.Cos (degToRad (90.833)) / (Math.Cos (latRad) * Math.Cos (sdRad)) - Math.Tan (latRad) * Math.Tan (sdRad));

            return -HA; // en radianes
        }

        /// <summary>
        /// Calcula el Tiempo Universal Coordinado (UTC) de la salida del sol para el día dado en el lugar dado en la tierra
        /// </summary>
        /// <param name="JD">día juliano</param>
        /// <param name="latitude">Latitud del observador en grados</param>
        /// <param name="longitude">Longitud del observador en grados</param>
        /// <returns>tiempo en minutos</returns>
        private static double calcSunriseUTC (double JD, double latitude, double longitude, double UTC_GMT) {
            var t = calcTimeJulianCent (JD);

            // Encuentra la hora del mediodía solar en la ubicación y usa
            // esa declinación. Esto es mejor que el inicio del día juliano

            var noonmin = calcSolNoonUTC (t, longitude, UTC_GMT);
            var tnoon = calcTimeJulianCent (JD + noonmin / 1440.0);

            // Primer paso para aproximarse a la salida del sol (usando mediodía solar)

            var eqTime = calcEquationOfTime (tnoon);
            var solarDec = calcSunDeclination (tnoon);
            var hourAngle = calcHourAngleSunrise (latitude, solarDec);

            var delta = longitude - radToDeg (hourAngle);
            var timeDiff = 4 * delta; // en minutos de tiempo
            var timeUTC = 720 + timeDiff - eqTime; // en minutos

            // Incluimos jday fraccional en gamma calc

            var newt = calcTimeJulianCent (calcJDFromJulianCent (t) + timeUTC / 1440.0);
            eqTime = calcEquationOfTime (newt);
            solarDec = calcSunDeclination (newt);
            hourAngle = calcHourAngleSunrise (latitude, solarDec);
            delta = longitude - radToDeg (hourAngle);
            timeDiff = 4 * delta;
            timeUTC = 720 + timeDiff - eqTime; // en minutos
            timeUTC += UTC_GMT * 60;

            return timeUTC;
        }

        /// <summary>
        /// Calcula el Tiempo Universal Coordinado (UTC) del mediodía solar para el día en el lugar dado en la tierra
        /// </summary>
        /// <param name="t">número de siglos julianos desde J2000.0</param>
        /// <param name="longitude">Longitud del observador en grados</param>
        /// <returns>solNoonUTC: tiempo en minutos</returns>
        private static double calcSolNoonUTC (double t, double longitude, double UTC_GMT) {
            // Se utiliza el mediodía solar aproximado para calcular la ecuación del tiempo
            var tnoon = calcTimeJulianCent (calcJDFromJulianCent (t) + longitude / 360.0);
            var eqTime = calcEquationOfTime (tnoon);
            var solNoonUTC = 720 + longitude * 4 - eqTime; // min

            var newt = calcTimeJulianCent (calcJDFromJulianCent (t) - 0.5 + solNoonUTC / 1440.0);

            eqTime = calcEquationOfTime (newt);
            solNoonUTC = 720 + longitude * 4 - eqTime; // min
            solNoonUTC += UTC_GMT * 60;

            return solNoonUTC;
        }

        /// <summary>
        /// Calcula el Tiempo Universal Coordinado (UTC) de la puesta del sol para el día dado en la ubicación dada en la tierra
        /// </summary>
        /// <param name="JD">día juliano</param>
        /// <param name="latitude">Latitud del observador en grados</param>
        /// <param name="longitude">Longitud del observador en grados</param>
        /// <returns>timeUTC: Tiempo en minutos</returns>
        private static double calcSunsetUTC (double JD, double latitude, double longitude, double UTC_GMT) {
            var t = calcTimeJulianCent (JD);

            // Encuentra la hora del mediodía solar en la ubicación y usa
            // esa declinación. Esto es mejor que el inicio del día juliano

            var noonmin = calcSolNoonUTC (t, longitude, UTC_GMT);
            var tnoon = calcTimeJulianCent (JD + noonmin / 1440.0);

            // Primero calcula el amanecer y la duración aproximada del día.

            var eqTime = calcEquationOfTime (tnoon);
            var solarDec = calcSunDeclination (tnoon);
            var hourAngle = calcHourAngleSunset (latitude, solarDec);

            var delta = longitude - radToDeg (hourAngle);
            var timeDiff = 4 * delta;
            var timeUTC = 720 + timeDiff - eqTime;

            // Primer paso usado para incluir el día fraccionario en el gamma calc.

            var newt = calcTimeJulianCent (calcJDFromJulianCent (t) + timeUTC / 1440.0);
            eqTime = calcEquationOfTime (newt);
            solarDec = calcSunDeclination (newt);
            hourAngle = calcHourAngleSunset (latitude, solarDec);

            delta = longitude - radToDeg (hourAngle);
            timeDiff = 4 * delta;
            timeUTC = 720 + timeDiff - eqTime; // en minutos
            timeUTC += UTC_GMT * 60;

            return timeUTC;
        }

        /// <summary>
        /// Calcula el día juliano del amanecer del sol más reciente a partir del día dado en el lugar dado en la tierra
        /// </summary>
        /// <param name="jd">dia juliano</param>
        /// <param name="latitude">latitud del observador en grados</param>
        /// <param name="longitude">longitud del observador en grados</param>
        /// <returns>julianday: dia juliano del amanecer de sol reciente</returns>
        private static double findRecentSunrise (double jd, double latitude, double longitude, double UTC_GMT) {
            var julianday = jd;

            var time = calcSunriseUTC (julianday, latitude, longitude, UTC_GMT);
            while (!isNumber (time)) {
                julianday -= 1.0;
                time = calcSunriseUTC (julianday, latitude, longitude, UTC_GMT);
            }

            return julianday;
        }

        /// <summary>
        /// Calcula el día juliano de la puesta del sol más reciente a partir del día dado en el lugar dado en la tierra
        /// </summary>
        /// <param name="jd">dia juliano</param>
        /// <param name="latitude">latitud del observador en grados</param>
        /// <param name="longitude">longitud del observador en grados</param>
        /// <returns>julianday: dia juliano de la puesta de sol reciente</returns>
        private static double findRecentSunset (double jd, double latitude, double longitude, double UTC_GMT) {
            var julianday = jd;

            var time = calcSunsetUTC (julianday, latitude, longitude, UTC_GMT);
            while (!isNumber (time)) {
                julianday -= 1.0;
                time = calcSunsetUTC (julianday, latitude, longitude, UTC_GMT);
            }

            return julianday;
        }

        /// <summary>
        /// Calcula el día juliano del proximo amanecer del sol a partir del día dado en el lugar dado en la tierra
        /// </summary>
        /// <param name="jd">dia juliano</param>
        /// <param name="latitude">latitud del observador en grados</param>
        /// <param name="longitude">longitud del observador en grados</param>
        /// <returns>julianday: dia juliano del próximo amanecer del sol</returns>
        private static double findNextSunrise (double jd, double latitude, double longitude, double UTC_GMT) {
            var julianday = jd;

            var time = calcSunriseUTC (julianday, latitude, longitude, UTC_GMT);
            while (!isNumber (time)) {
                julianday += 1.0;

                time = calcSunriseUTC (julianday, latitude, longitude, UTC_GMT);

            }

            return julianday;
        }

        /// <summary>
        /// Calcula el día juliano de la próxima puesta de sol a partir del día dado en el lugar dado en la tierra
        /// </summary>
        /// <param name="jd">dia juliano</param>
        /// <param name="latitude">latitud del observador en grados</param>
        /// <param name="longitude">longitud del observador en grados</param>
        /// <returns>julianday: dia juliano de la próxima puesta de sol</returns>
        private static double findNextSunset (double jd, double latitude, double longitude, double UTC_GMT) {
            var julianday = jd;

            var time = calcSunsetUTC (julianday, latitude, longitude, UTC_GMT);
            while (!isNumber (time)) {
                julianday += 1.0;
                time = calcSunsetUTC (julianday, latitude, longitude, UTC_GMT);
            }

            return julianday;
        }
    }
}