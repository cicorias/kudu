﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.SiteExtensions;
using Kudu.Services.Arm;

namespace Kudu.Services.SiteExtensions
{
    public class SiteExtensionController : ApiController
    {
        private readonly ISiteExtensionManager _manager;

        public SiteExtensionController(ISiteExtensionManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false, string feedUrl = null)
        {
            return await _manager.GetRemoteExtensions(filter, allowPrereleaseVersions, feedUrl);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version = null, string feedUrl = null)
        {
            SiteExtensionInfo extension = await _manager.GetRemoteExtension(id, version, feedUrl);

            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }

            return extension;
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter = null, bool checkLatest = true)
        {
            return await _manager.GetLocalExtensions(filter, checkLatest);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            SiteExtensionInfo extension = await _manager.GetLocalExtension(id, checkLatest);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }
            return extension;
        }

        [HttpPut]
        public async Task<HttpResponseMessage> InstallExtension(string id, SiteExtensionInfo requestInfo)
        {
            var startTime = DateTime.UtcNow;
            if (requestInfo == null)
            {
                requestInfo = new SiteExtensionInfo();
            }

            SiteExtensionInfo result;

            if (ArmUtils.IsArmRequest(Request))
            {
                result = await _manager.InitInstallSiteExtension(id);

                // trigger installation, but do not wait. Expecting caller to poll
#pragma warning disable 4014
                _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl);
#pragma warning restore 4014

                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(result, Request));
            }
            else
            {
                try
                {
                    result = await _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl);
                }
                catch (WebException e)
                {
                    // This can happen for example if a bad feed URL is passed
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Site extension download failure", e));
                }
                catch (Exception e)
                {
                    // This can happen for example if the exception package is corrupted
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Site extension install exception. The package might be invalid.", e));
                }

                if (result == null)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, "Could not find " + id));
                }

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
        }

        [HttpDelete]
        public async Task<bool> UninstallExtension(string id)
        {
            try
            {
                return await _manager.UninstallExtension(id);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }
        }
    }
}
