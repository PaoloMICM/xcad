﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using System;
using System.Collections.Generic;
using System.Text;

namespace Xarial.XCad.Documents
{
    /// <summary>
    /// Represents length unit
    /// </summary>
    public enum Length_e 
    {
        /// <summary>
        /// Å
        /// </summary>
        Angstroms,

        /// <summary>
        /// nm
        /// </summary>
        Nanometers,

        /// <summary>
        /// μ
        /// </summary>
        Microns,

        /// <summary>
        /// mm
        /// </summary>
        Millimeters,

        /// <summary>
        /// cm
        /// </summary>
        Centimeters,

        /// <summary>
        /// m
        /// </summary>
        Meters,

        /// <summary>
        /// µ"
        /// </summary>
        Microinches,

        /// <summary>
        /// mil
        /// </summary>
        Mils,

        /// <summary>
        /// "
        /// </summary>
        Inches,

        /// <summary>
        /// ft
        /// </summary>
        Feet 
    }

    /// <summary>
    /// Represents mass units
    /// </summary>
    public enum Mass_e
    {
        /// <summary>
        /// mg
        /// </summary>
        Milligrams,

        /// <summary>
        /// g
        /// </summary>
        Grams,

        /// <summary>
        /// kg
        /// </summary>
        Kilograms,

        /// <summary>
        /// lb
        /// </summary>
        Pounds,
    }
    
    /// <summary>
    /// Represents angle units
    /// </summary>
    public enum Angle_e
    {
        /// <summary>
        /// °
        /// </summary>
        Degrees,
        
        /// <summary>
        /// rad
        /// </summary>
        Radians
    }

    /// <summary>
    /// Represents time units
    /// </summary>
    public enum Time_e
    {
        /// <summary>
        /// sec
        /// </summary>
        Seconds,

        /// <summary>
        /// msec
        /// </summary>
        Milliseconds,

        /// <summary>
        /// µsec
        /// </summary>
        Microseconds,

        /// <summary>
        /// nsec
        /// </summary>
        Nanoseconds,

        /// <summary>
        /// min
        /// </summary>
        Minutes,

        /// <summary>
        /// hr
        /// </summary>
        Hours
    }

    /// <summary>
    /// Represents the units system of the document
    /// </summary>
    public interface IXUnits
    {
        /// <summary>
        /// Acessing length units
        /// </summary>
        Length_e Length { get; set; }

        /// <summary>
        /// Acessing mass units
        /// </summary>
        Mass_e Mass { get; set; }

        /// <summary>
        /// Acessing angle units
        /// </summary>
        Angle_e Angle { get; set; }

        /// <summary>
        /// Acessing time units
        /// </summary>
        Time_e Time { get; set; }
    }

    /// <summary>
    /// Additional methods of <see cref="IXUnits"/>
    /// </summary>
    public static class XUnitsExtension 
    {
        private static readonly Dictionary<Length_e, double> m_LengthConvFactor = new Dictionary<Length_e, double>()
        {
            { Length_e.Angstroms, 1e+10 },
            { Length_e.Nanometers, 1e+9  },
            { Length_e.Microns, 1000000 },
            { Length_e.Millimeters, 1000 },
            { Length_e.Centimeters, 100 },
            { Length_e.Meters, 1 },
            { Length_e.Microinches, 39370078.740157485 },
            { Length_e.Mils, 39370.078740157 },
            { Length_e.Inches, 39.3700787402 },
            { Length_e.Feet, 3.280839895 }
        };

        private static readonly Dictionary<Mass_e, double> m_MassConvFactor = new Dictionary<Mass_e, double>()
        {
            { Mass_e.Milligrams, 1000000 },
            { Mass_e.Grams, 1000 },
            { Mass_e.Kilograms, 1 },
            { Mass_e.Pounds, 2.2046226218 }
        };

        private static readonly Dictionary<Angle_e, double> m_AngleConvFactor = new Dictionary<Angle_e, double>()
        {
            { Angle_e.Degrees, 180 / Math.PI },
            { Angle_e.Radians, 1 }
        };

        private static readonly Dictionary<Time_e, double> m_TimeConvFactor = new Dictionary<Time_e, double>()
        {
            { Time_e.Seconds, 1 },
            { Time_e.Milliseconds, 1000 },
            { Time_e.Microseconds, 1000000 },
            { Time_e.Nanoseconds, 1e+9 },
            { Time_e.Minutes, 1 / 60 },
            { Time_e.Hours, 1 / 3600 }
        };

        /// <summary>
        /// Gets the length conversion factor from system units (meters) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <returns>Conversion factor</returns>
        public static double GetLengthConversionFactor(this IXUnits unit)
            => m_LengthConvFactor[unit.Length];

        /// <summary>
        /// Gets the mass conversion factor from system units (kilograms) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <returns>Conversion factor</returns>
        public static double GetMassConversionFactor(this IXUnits unit)
            => m_MassConvFactor[unit.Mass];

        /// <summary>
        /// Gets the angle conversion factor from system units (radians) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <returns>Conversion factor</returns>
        public static double GetAngleConversionFactor(this IXUnits unit)
            => m_AngleConvFactor[unit.Angle];

        /// <summary>
        /// Gets the time conversion factor from system units (seconds) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <returns>Conversion factor</returns>
        public static double GetTimeConversionFactor(this IXUnits unit)
            => m_TimeConvFactor[unit.Time];

        /// <summary>
        /// Converts the length value from the user units to system units (meters)
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="userValue">User value</param>
        /// <returns>Equivalent system value of length (meters)</returns>
        public static double ConvertLengthToSystemValue(this IXUnits unit, double userValue)
            => unit.GetLengthConversionFactor() / userValue;

        /// <summary>
        /// Converts the length value from the system units (meters) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="systemValue">System value of length (meters)</param>
        /// <returns>Equivalent user value</returns>
        public static double ConvertLengthToUserValue(this IXUnits unit, double systemValue)
            => unit.GetLengthConversionFactor() * systemValue;

        /// <summary>
        /// Converts the mass value from the user unit to system units (kilograms)
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="userValue">User value</param>
        /// <returns>Equivalent system value of mass (kilograms)</returns>
        public static double ConvertMassToSystemValue(this IXUnits unit, double userValue)
            => unit.GetMassConversionFactor() / userValue;

        /// <summary>
        /// Converts the mass value from the system units (kilograms) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="systemValue">System value of mass (kilograms)</param>
        /// <returns>Equivalent user value</returns>
        public static double ConvertMassToUserValue(this IXUnits unit, double systemValue)
            => unit.GetMassConversionFactor() * systemValue;

        /// <summary>
        /// Converts the angle value from the user unit to system units (radians)
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="userValue">User value</param>
        /// <returns>Equivalent system value of angle (radians)</returns>
        public static double ConvertAngleToSystemValue(this IXUnits unit, double userValue)
            => unit.GetAngleConversionFactor() / userValue;

        /// <summary>
        /// Converts the angle value from the system units (radians) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="systemValue">System value of angle (radians)</param>
        /// <returns>Equivalent user value</returns>
        public static double ConvertAngleToUserValue(this IXUnits unit, double systemValue)
            => unit.GetAngleConversionFactor() * systemValue;

        /// <summary>
        /// Converts the time value from the user unit to system units (seconds)
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="userValue">User value</param>
        /// <returns>Equivalent system value of time (seconds)</returns>
        public static double ConvertTimeToSystemValue(this IXUnits unit, double userValue)
            => unit.GetTimeConversionFactor() / userValue;

        /// <summary>
        /// Converts the time value from the system units (seconds) to user units
        /// </summary>
        /// <param name="unit">Units</param>
        /// <param name="systemValue">System value of time (seconds)</param>
        /// <returns>Equivalent user value</returns>
        public static double ConvertTimeToUserValue(this IXUnits unit, double systemValue)
            => unit.GetTimeConversionFactor() * systemValue;
    }
}
