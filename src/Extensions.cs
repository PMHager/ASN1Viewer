// Copyright (c) 2026 Peter-Michael Hager. All rights reserved.
// Proprietary and confidential. Shared solely for evaluation purposes.
// Intended to be released under an open-source license upon grant approval.

using System.Collections;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace Asn1View
{
    internal static partial class StringExtensions
    {
        public static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);
        public static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
        public static string WrapInQuotes<T>(this T str) => $"\"{str}\"";
        public static string UnwrapQuotes(this string str) => str.Length > 1 && str.StartsWith('"') && str.EndsWith('"') ? str[1..^1] : str;
        public static string WrapInParentheses<T>(this T str) => $"({str})";
        public static string UnwrapParentheses(this string str) => str.Length > 1 && str.StartsWith('(') && str.EndsWith(')') ? str[1..^1] : str;
        public static string WrapInBrackets<T>(this T str) => $"[{str}]";
        public static string UnwrapBrackets(this string str) => str.Length > 1 && str.StartsWith('[') && str.EndsWith(']') ? str[1..^1] : str;
        public static string WrapInBraces<T>(this T str) => $"{{{str}}}";
        public static string UnwrapBraces(this string str) => str.Length > 1 && str.StartsWith('{') && str.EndsWith('}') ? str[1..^1] : str;
        public static string LimitLength(this string str, int length, int last) =>
            str.Length > length && length > last && length > 0 && last >= 0
            ? string.Concat(str.AsSpan(0, length - 1 - last), "…", str.AsSpan(str.Length - last))
            : str;
        public static string WithoutMnemonicAndDots(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            StringBuilder sb = new(str);
            sb.Replace("&&", "\0");
            sb.Replace("&", null);
            sb.Replace("…", null);
            sb.Replace('\0', '&');
            return sb.ToString();
        }
        public static string ToSubscript(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return string.Create(str.Length, str, static (span, str) =>
            {
                int i = 0;
                foreach (char chr in str)
                {
                    span[i++] = chr switch
                    {
                        '0' => '₀',
                        '1' => '₁',
                        '2' => '₂',
                        '3' => '₃',
                        '4' => '₄',
                        '5' => '₅',
                        '6' => '₆',
                        '7' => '₇',
                        '8' => '₈',
                        '9' => '₉',
                        '+' => '₊',
                        '-' => '₋',
                        '=' => '₌',
                        '(' => '₍',
                        ')' => '₎',
                        _ => chr
                    };
                }
            });
        }
        public static string ToSuperscript(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return string.Create(str.Length, str, static (span, str) =>
            {
                int i = 0;
                foreach (char chr in str)
                {
                    span[i++] = chr switch
                    {
                        '0' => '⁰',
                        '1' => '¹',
                        '2' => '²',
                        '3' => '³',
                        '4' => '⁴',
                        '5' => '⁵',
                        '6' => '⁶',
                        '7' => '⁷',
                        '8' => '⁸',
                        '9' => '⁹',
                        '+' => '⁺',
                        '-' => '⁻',
                        '=' => '⁼',
                        '(' => '⁽',
                        ')' => '⁾',
                        _ => chr
                    };
                }
            });
        }
    }
    internal static partial class SpanExtensions
    {
        public static Encoding DetectEncoding(this ReadOnlySpan<byte> bytes)
        {
            //return bytes.Take(4).ToArray().DetectEncoding();
            Encoding encoding;
            if (4 <= bytes.Length && 0xFF == bytes[0] && 0xFE == bytes[1] && 0x00 == bytes[2] && 0x00 == bytes[3])      // UTF32LE: BOM = FF FE 00 00 
                encoding = Encoding.UTF32;
            else if (4 <= bytes.Length && 0x00 == bytes[0] && 0x00 == bytes[1] && 0xFE == bytes[2] && 0xFF == bytes[3]) // UTF32BE: BOM = 00 00 FE FF 
                encoding = Encoding.GetEncoding(12001);
            else if (2 <= bytes.Length && 0xFF == bytes[0] && 0xFE == bytes[1])                                         // UTF16LE: BOM = FF FE 
                encoding = Encoding.Unicode;
            else if (2 <= bytes.Length && 0xFE == bytes[0] && 0xFF == bytes[1])                                         // UTF16BE: BOM = FE FF 
                encoding = Encoding.BigEndianUnicode;
            else if (3 <= bytes.Length && 0xEF == bytes[0] && 0xBB == bytes[1] && 0xBF == bytes[2])                     // UTF8:    BOM = EF BB BF 
                encoding = Encoding.UTF8;
            else if (3 <= bytes.Length && 0x2B == bytes[0] && 0x2F == bytes[1] && 0x76 == bytes[2])                     // UTF7:    BOM = 2B 2F 76
                encoding = Encoding.GetEncoding(65000);
            else
                encoding = Encoding.Default;                                                                            // ANSI as default
            return encoding;
        }
        public static string LimitLength(this ReadOnlySpan<char> str, int length, int last) =>
            str.Length > length && length > last && length > 0 && last >= 0
            ? string.Concat(str[..(length - 1 - last)], "…", str[^last..])
            : str.ToString();
        public static string ToHex(this ReadOnlySpan<byte> bytes, bool lowercase = false, string delimiter = "")
        {
            if (bytes.IsEmpty)
                return string.Empty;
            string hex = lowercase
                ? Convert.ToHexString(bytes).ToLowerInvariant() // .NET 9+: Convert.ToHexStringLower(bytes)
                : Convert.ToHexString(bytes);
            if (!string.IsNullOrEmpty(delimiter))
                hex = string.Join(delimiter, Enumerable.Range(0, bytes.Length).Select(i => hex.Substring(i * 2, 2)));
            return hex;
        }
        public static BigInteger ToBigInteger(this ReadOnlySpan<byte> bytes, bool isUnsigned = false, bool isBigEndian = true)
        {
            return new BigInteger(bytes, isUnsigned, isBigEndian);
        }
        public static bool All<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
        {
            foreach (var item in span)
            {
                if (!predicate(item))
                {
                    return false;
                }
            }
            return true;
        }
    }
    public static partial class ObjectExtensions
    {
        public static bool In<T>(this T value, params ReadOnlySpan<T> items) where T : IEquatable<T>
        {
            return items.Contains(value);
        }
        public static bool In<T>(this T value, params T[] items)
        {
            return items.Contains(value);
        }
    }
    internal static partial class StructExtensions
    {
        public static T[] Copy<T>(this T[] sourceArray, int offset, int length) where T : struct
        {
            T[] destinationArray = new T[length];
            Array.Copy(sourceArray, offset, destinationArray, 0, length);
            return destinationArray;
        }
        public static string GetDescription<T>(this T obj) where T : struct
        {
            string description = obj.ToString() ?? string.Empty;
            FieldInfo? fieldInfo = obj.GetType().GetField(description);
            if (fieldInfo is not null)
            {
                object[] attributes = fieldInfo.GetCustomAttributes(false);
                if (0 < attributes.Length && attributes[0] is DescriptionAttribute descriptionAttribute)
                {
                    description = descriptionAttribute.Description;
                }
            }
            return description;
        }
    }
    public static partial class TreeNodeExtensions
    {
        public static void ExpandAllBottomUp(this TreeView treeView)
        {
            foreach (TreeNode root in treeView.Nodes)
            {
                root.ExpandAllBottomUp();
            }
        }
        public static void ExpandAllBottomUp(this TreeNode node)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.ExpandAllBottomUp();
            }
            if (node.Nodes.Count > 0 && !node.IsExpanded)
            {
                node.Expand();
            }
        }
        public static void CollapseAllFast(this TreeView treeView)
        {
            foreach (TreeNode child in treeView.Nodes)
            {  
                child.CollapseAllFast(); 
            }
        }
        public static void CollapseAllFast(this TreeNode node)
        {
            node.Collapse(true);
            Stack<TreeNode> stack = new();
            foreach (TreeNode child in node.Nodes)
            {
                stack.Push(child);
            }
            while (stack.Count > 0)
            {
                TreeNode current = stack.Pop();
                if (current.IsExpanded)
                {
                    current.Collapse(true);
                }
                foreach (TreeNode child in current.Nodes)
                {
                    stack.Push(child);
                }
            }
        }
    }
    public static partial class ColorExtensions
    {
        public static Color Inverted(this Color color)
        {
            return Color.FromArgb(color.A, 255 - color.R, 255 - color.G, 255 - color.B);
        }
        public static Color IntensifyMax(this Color color)
        {
            int r = color.R;
            int g = color.G;
            int b = color.B;
            int max = Math.Max(Math.Max(r, g), b);
            return (max == 0) ? color : Color.FromArgb(color.A, (r * 255 + (max >> 1)) / max, (g * 255 + (max >> 1)) / max, (b * 255 + (max >> 1)) / max);
        }
        public static Color IntensifyForDarkMode(this Color color, float lightnessBonus = 0.2f)
        {
            float h = color.GetHue();               // 0-360
            float s = color.GetSaturation();        // 0-1
            float l = color.GetBrightness();        // 0-1 (Lightness)
            l = Math.Min(1.0f, l + lightnessBonus); // Intensify up to white
            float v, r, g, b;
            r = g = b = l;
            v = (l <= 0.5f) ? (l * (1.0f + s)) : (l + s - l * s);
            if (v > 0)
            {
                float m = l + l - v;
                float sv = (v - m) / v;
                h /= 60.0f;
                int sextant = (int)h;
                float fract = h - sextant;
                float vsf = v * sv * fract;
                float mid1 = m + vsf;
                float mid2 = v - vsf;
                switch (sextant)
                {
                    case 0: r = v; g = mid1; b = m; break;
                    case 1: r = mid2; g = v; b = m; break;
                    case 2: r = m; g = v; b = mid1; break;
                    case 3: r = m; g = mid2; b = v; break;
                    case 4: r = mid1; g = m; b = v; break;
                    case 5: r = v; g = m; b = mid2; break;
                }
            }
            return Color.FromArgb(color.A, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
        public static Color LightenWithWhite(this Color color, float amount = 0.3f)
        {
            int r = (int)(color.R + (255 - color.R) * amount);
            int g = (int)(color.G + (255 - color.G) * amount);
            int b = (int)(color.B + (255 - color.B) * amount);
            return Color.FromArgb(color.A, r, g, b);
        }
        public static Color DarkenWithBlack(this Color color, float amount = 0.3f)
        {
            // amount = 0.0 (Original) bis 1.0 (Komplett Schwarz)
            int r = (int)(color.R * (1.0f - amount));
            int g = (int)(color.G * (1.0f - amount));
            int b = (int)(color.B * (1.0f - amount));

            return Color.FromArgb(color.A, r, g, b);
        }
    }
    public static partial class ObjectMergerExtensions
    {
        public static void MergeNonNull(this object target, object source)
        {
            if (target == null || source == null) return;
            Type type = target.GetType(); // Get runtime type
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                var sourceValue = prop.GetValue(source);
                if (sourceValue == null) continue;
                if (sourceValue is string sourceStr && string.IsNullOrWhiteSpace(sourceStr)) continue;
                var targetValue = prop.GetValue(target);
                if (sourceValue is IDictionary sourceDict && targetValue is IDictionary targetDict) // Inner Dictionary
                {
                    foreach (var key in sourceDict.Keys)
                    {
                        var sVal = sourceDict[key];
                        if (sVal == null) continue;
                        if (sVal is string sStr && string.IsNullOrWhiteSpace(sStr)) continue;
                        if (targetDict.Contains(key))
                        {
                            var tVal = targetDict[key];
                            if (tVal != null && !sVal.GetType().IsValueType && sVal is not string)
                            {
                                tVal.MergeNonNull(sVal);    // Recursion
                            }
                            else
                            {
                                targetDict[key] = sVal;
                            }
                        }
                        else
                        {
                            targetDict[key] = sVal;
                        }
                    }
                }
                else if (targetValue != null && !prop.PropertyType.IsValueType && prop.PropertyType != typeof(string)) // Structured sub-object
                {
                    targetValue.MergeNonNull(sourceValue);  // Recursion
                }
                else
                {
                    prop.SetValue(target, sourceValue);
                }
            }
        }
    }
}
