﻿using System;
using System.Collections.Generic;
using System.Web.Http;
using SiteServer.BackgroundPages.Core;
using SiteServer.CMS.Caches;
using SiteServer.CMS.Caches.Content;
using SiteServer.CMS.Core;
using SiteServer.CMS.Core.Create;
using SiteServer.CMS.Database.Attributes;
using SiteServer.Utils;

namespace SiteServer.API.Controllers.Pages.Cms
{
    [RoutePrefix("pages/cms/contentsLayerDelete")]
    public class PagesContentsLayerDeleteController : ApiController
    {
        private const string Route = "";

        [HttpGet, Route(Route)]
        public IHttpActionResult GetConfig()
        {
            try
            {
                var rest = new Rest(Request);

                var siteId = rest.GetQueryInt("siteId");
                var channelId = rest.GetQueryInt("channelId");
                var contentIdList = TranslateUtils.StringCollectionToIntList(rest.GetQueryString("contentIds"));

                if (!rest.IsAdminLoggin ||
                    !rest.AdminPermissionsImpl.HasChannelPermissions(siteId, channelId,
                        ConfigManager.ChannelPermissions.ContentDelete))
                {
                    return Unauthorized();
                }

                var siteInfo = SiteManager.GetSiteInfo(siteId);
                if (siteInfo == null) return BadRequest("无法确定内容对应的站点");

                var channelInfo = ChannelManager.GetChannelInfo(siteId, channelId);
                if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

                var retVal = new List<Dictionary<string, object>>();
                foreach (var contentId in contentIdList)
                {
                    var contentInfo = ContentManager.GetContentInfo(siteInfo, channelInfo, contentId);
                    if (contentInfo == null) continue;

                    var dict = new Dictionary<string, object>(contentInfo.ToDictionary())
                    {
                        {"title", WebUtils.GetContentTitle(siteInfo, contentInfo, string.Empty)},
                        {"checkState", CheckManager.GetCheckState(siteInfo, contentInfo)}
                    };
                    retVal.Add(dict);
                }

                return Ok(new
                {
                    Value = retVal
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(Route)]
        public IHttpActionResult Submit()
        {
            try
            {
                var rest = new Rest(Request);

                var siteId = rest.GetPostInt("siteId");
                var channelId = rest.GetPostInt("channelId");
                var contentIdList = TranslateUtils.StringCollectionToIntList(rest.GetPostString("contentIds"));
                var isRetainFiles = rest.GetPostBool("isRetainFiles");

                if (!rest.IsAdminLoggin ||
                    !rest.AdminPermissionsImpl.HasChannelPermissions(siteId, channelId,
                        ConfigManager.ChannelPermissions.ContentDelete))
                {
                    return Unauthorized();
                }

                var siteInfo = SiteManager.GetSiteInfo(siteId);
                if (siteInfo == null) return BadRequest("无法确定内容对应的站点");

                var channelInfo = ChannelManager.GetChannelInfo(siteId, channelId);
                if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

                if (!isRetainFiles)
                {
                    DeleteManager.DeleteContents(siteInfo, channelId, contentIdList);
                }

                //var tableName = ChannelManager.GetTableName(siteInfo, channelInfo);

                if (contentIdList.Count == 1)
                {
                    var contentId = contentIdList[0];

                    if (channelInfo.ContentRepository.GetChanelIdAndValue<string>(contentId, ContentAttribute.Title,
                        out var contentChannelId, out var contentTitle))
                    {
                        rest.AddSiteLog(siteId, contentChannelId, contentId, "删除内容",
                            $"栏目:{ChannelManager.GetChannelNameNavigation(siteId, contentChannelId)},内容标题:{contentTitle}");
                    }
                }
                else
                {
                    rest.AddSiteLog(siteId, "批量删除内容",
                        $"栏目:{ChannelManager.GetChannelNameNavigation(siteId, channelId)},内容条数:{contentIdList.Count}");
                }

                channelInfo.ContentRepository.UpdateTrashContents(siteId, channelId, contentIdList);

                CreateManager.TriggerContentChangedEvent(siteId, channelId);

                return Ok(new
                {
                    Value = contentIdList
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }
    }
}
