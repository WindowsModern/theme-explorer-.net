using System;
using System.Globalization;
using System.Threading;

namespace ThemeExplorer.Ui
{
    /// <summary>
    /// Keeps track of the UI language. English is the neutral resource language; Simplified Chinese lives in the zh-Hans satellite assembly.
    /// <para xml:lang="zh-CN">跟踪界面语言。英语是中性资源语言；简体中文位于 zh-Hans 卫星程序集。</para>
    /// </summary>
    public static class UiCulture
    {
        private const string SimplifiedChinese = "zh-Hans";

        public static CultureInfo Current
        {
            get { return Thread.CurrentThread.CurrentUICulture; }
        }

        public static bool IsChinese
        {
            get { return Current.TwoLetterISOLanguageName == "zh"; }
        }

        /// <summary>
        /// Applies the language selected on the command line (<c>--lang=en</c> or <c>--lang=zh-Hans</c>), otherwise follows the operating system. All Chinese locales map to zh-Hans because that is the only translation we ship.
        /// <para xml:lang="zh-CN">应用命令行选择的语言（<c>--lang=en</c> 或 <c>--lang=zh-Hans</c>），否则跟随操作系统。所有中文区域都映射到 zh-Hans，因为这是我们唯一提供的翻译。</para>
        /// </summary>
        public static void Initialize(string[] args)
        {
            string requested = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg.Equals("--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        requested = args[++i];
                    }
                    else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                    {
                        requested = arg.Substring("--lang=".Length);
                    }
                }
            }

            CultureInfo culture = null;
            if (!string.IsNullOrEmpty(requested))
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(requested);
                }
                catch (CultureNotFoundException)
                {
                    culture = null;
                }
            }

            if (culture == null)
                culture = Current;

            Set(Normalize(culture));
        }

        public static void Set(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        public static CultureInfo Normalize(CultureInfo culture)
        {
            if (culture != null && culture.TwoLetterISOLanguageName == "zh")
                return CultureInfo.GetCultureInfo(SimplifiedChinese);
            return culture ?? CultureInfo.GetCultureInfo("en");
        }
    }
}
