using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Web.Models.Segments;

namespace Umbraco.Web.Routing.Segments
{
    [DisplayName("Browser width provider")]
    [Description("A provider that relies on JavaScript to ensure that the screen width is set as a segment on a request")]
    public class BrowserWidthProvider : ConfigurableSegmentProvider
    {
        /// <summary>
        /// Performs a custom match/comparison
        /// </summary>
        /// <param name="matchStatement"></param>
        /// <param name="cleanedRequestUrl"></param>
        /// <param name="httpRequest"></param>
        /// <returns></returns>
        /// <remarks>
        /// <![CDATA[
        /// Supports syntax like:
        /// 
        /// = 100
        /// 100
        /// >= 100
        /// <= 100
        /// 
        /// does not support not equals.
        /// ]]>
        /// </remarks>
        public override bool IsMatch(string matchStatement, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
        {
            if (matchStatement == null) throw new ArgumentNullException("matchStatement");
            if (cleanedRequestUrl == null) throw new ArgumentNullException("cleanedRequestUrl");
            if (httpRequest == null) throw new ArgumentNullException("httpRequest");

            var val = GetCurrentValue(cleanedRequestUrl, httpRequest) as int?;
            if (val == null) return false;

            matchStatement = matchStatement.Replace(" ", "");

            var first = matchStatement[0];
            var second = matchStatement[1];

            switch (first)
            {
                case '=':
                    var a = matchStatement.TrimStart('=').TryConvertTo<int>();
                    if (a == false) return false;
                    return val == a.Result;   
                case '>':
                    if (second == '=')
                    {
                        var b = matchStatement.TrimStart('>', '=').TryConvertTo<int>();
                        if (b == false) return false;
                        return val >= b.Result;
                    }
                    var c = matchStatement.TrimStart('>').TryConvertTo<int>();
                    if (c == false) return false;
                    return val > c.Result;
                case '<':
                    if (second == '=')
                    {
                        var d = matchStatement.TrimStart('<', '=').TryConvertTo<int>();
                        if (d == false) return false;
                        return val <= d.Result;
                    }
                    var e = matchStatement.TrimStart('<').TryConvertTo<int>();
                    if (e == false) return false;
                    return val < e.Result;
                default:
                    //we'll assume it means equal
                    var f = matchStatement.TryConvertTo<int>();
                    if (f == false) return false;
                    return val == f.Result;  
            }
        }

        public override object GetCurrentValue(Uri cleanedRequestUrl, HttpRequestBase httpRequest)
        {
            var cookie = httpRequest.Cookies["__ubw__"];
            if (cookie == null) return null;

            var val = cookie.Value.TryConvertTo<int>();
            if (val == false) return null;

            return val.Result;
        }
    }
}