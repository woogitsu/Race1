using System;
using System.Globalization;

namespace Race1.Core
{
    /// <summary>
    /// File names for photo-mode screenshots. They are sortable by time and use fixed digits in every culture.
    /// A numeric suffix, from a caller-supplied counter, keeps shots taken in the same second from overwriting each other.
    /// </summary>
    public static class PhotoShots
    {
        public const string Prefix = "Race1_";
        public const string Extension = ".png";

        /// <param name="local">Local time of the shot.</param>
        /// <param name="index">Zero for no suffix. Otherwise it is appended, so pass a counter to keep names unique within a second.</param>
        public static string FileName(DateTime local, int index)
        {
            string stamp = local.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string suffix = index > 0 ? "_" + index.ToString(CultureInfo.InvariantCulture) : string.Empty;
            return Prefix + stamp + suffix + Extension;
        }
    }
}
