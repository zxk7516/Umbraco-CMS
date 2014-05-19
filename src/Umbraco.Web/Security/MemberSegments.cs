using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web.Routing.Segments;

namespace Umbraco.Web.Security
{
    /// <summary>
    /// This is similar tothe RequestSegments class, however the member segments is not just based on current request data, this will check for the segments
    /// based on the current request, the segments cookie and finally the member instance.
    /// </summary>
    /// <remarks>
    /// Ensure this class is only created in a Request scope!
    /// </remarks>
    public class MemberSegments
    {
        private readonly MembershipHelper _membershipHelper;
        private readonly RequestSegments _reguestSegments;
        private readonly ServiceContext _services;

        public MemberSegments(MembershipHelper membershipHelper, RequestSegments reguestSegments, ServiceContext services)
        {
            if (membershipHelper == null) throw new ArgumentNullException("membershipHelper");
            if (reguestSegments == null) throw new ArgumentNullException("reguestSegments");
            if (services == null) throw new ArgumentNullException("services");
            _membershipHelper = membershipHelper;
            _reguestSegments = reguestSegments;
            _services = services;
        }

        //TODO: Add an GetAll method!

        public bool Is(string segmentKey)
        {
            //this will check the request (+ cookie)
            var contains = _reguestSegments.RequestContainsKey(segmentKey);
            //if the request has the key, then return from the request
            if (contains) return _reguestSegments.RequestIs(segmentKey);

            //lookup from member
            var fromMember = GetSegmentByKeyFromMember(segmentKey);
            return fromMember != null && fromMember.Value is bool && (bool)fromMember.Value;
        }

        public bool ContainsKey(string segmentKey)
        {
            //this will check the request (+ cookie)
            var contains = _reguestSegments.RequestContainsKey(segmentKey);
            //if the request has the key, then return from the request
            if (contains) return true;

            //lookup from member
            var fromMember = GetSegmentByKeyFromMember(segmentKey);
            return fromMember != null;
        }

        public bool ContainsValue(string segmentVal)
        {
            //this will check the request (+ cookie)
            var contains = _reguestSegments.RequestContainsValue(segmentVal);
            //if the request has the val, then return from the request
            if (contains) return true;

            //lookup from member
            var fromMember = PersistedSegments.FirstOrDefault(x => x.Value.ToString() == segmentVal);
            return fromMember != null;
        }

        public bool Equals(string segmentKey, object val)
        {
            //this will check the request (+ cookie)
            var contains = _reguestSegments.RequestContainsKey(segmentKey);
            //if the request has the key, then return from the request
            if (contains) return _reguestSegments.RequestEquals(segmentKey, val);

            //lookup from member
            var fromMember = GetSegmentByKeyFromMember(segmentKey);
            return fromMember != null && fromMember.Value == val;
        }

        private IMember _member;
        private bool _hasLoaded = false;
        /// <summary>
        /// Lazy loads and only loads once per request
        /// </summary>
        internal IMember CurrentMember
        {
            get
            {
                if (_member == null && _hasLoaded == false)
                {
                    var memberId = _membershipHelper.GetCurrentMemberId();
                    if (memberId > 0)
                    {
                        _member = _services.MemberService.GetById(memberId);
                    }
                    _hasLoaded = true;
                }
                return _member;
            }
        }

        private Segment[] _persistedSegments;
        /// <summary>
        /// Lazy loads the persisted once per request
        /// </summary>
        internal IEnumerable<Segment> PersistedSegments
        {
            get
            {
                if (_persistedSegments == null && CurrentMember != null && CurrentMember.HasProperty(Constants.Conventions.Member.Segments))
                {
                    var val = CurrentMember.Properties[Constants.Conventions.Member.Segments].Value.ToString();
                    _persistedSegments = JsonConvert.DeserializeObject<Segment[]>(val);
                }
                return (_persistedSegments == null || _persistedSegments.Length == 0) 
                    ? Enumerable.Empty<Segment>() 
                    : _persistedSegments;
            }
        } 

        private Segment GetSegmentByKeyFromMember(string segmentKey)
        {
            var segment = PersistedSegments.FirstOrDefault(x => x.Name == segmentKey);
            if (segment == null) return null;
            
            //add it to the request so we don't have to lookup in db again
            _reguestSegments.Add(segment);
            return segment;
        }
    }
}