﻿using System.Collections.Generic;
using System.Web.Mvc;
using MVCForum.Domain.Constants;
using MVCForum.Domain.DomainModel;
using MVCForum.Domain.Interfaces.Services;
using MVCForum.Domain.Interfaces.UnitOfWork;
using MVCForum.Website.Application;
using MVCForum.Website.ViewModels;
using System.Linq;

namespace MVCForum.Website.Controllers
{
    public class CategoryController : BaseController
    {
        private readonly ICategoryService _categoryService;
        private readonly ICategoryNotificationService _categoryNotificationService;
        private readonly ITopicService _topicService;

        private MembershipUser LoggedOnUser;
        private MembershipRole UsersRole;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggingService"> </param>
        /// <param name="unitOfWorkManager"> </param>
        /// <param name="membershipService"></param>
        /// <param name="localizationService"></param>
        /// <param name="roleService"></param>
        /// <param name="categoryService"></param>
        /// <param name="settingsService"> </param>
        /// <param name="topicService"> </param>
        /// <param name="categoryNotificationService"> </param>
        public CategoryController(ILoggingService loggingService, IUnitOfWorkManager unitOfWorkManager,
            IMembershipService membershipService,
            ILocalizationService localizationService,
            IRoleService roleService,
            ICategoryService categoryService,
            ISettingsService settingsService, ITopicService topicService, ICategoryNotificationService categoryNotificationService)
            : base(loggingService, unitOfWorkManager, membershipService, localizationService, roleService, settingsService)
        {
            _categoryService = categoryService;
            _topicService = topicService;
            _categoryNotificationService = categoryNotificationService;

            LoggedOnUser = UserIsAuthenticated ? MembershipService.GetUser(Username) : null;
            UsersRole = LoggedOnUser == null ? RoleService.GetRole(AppConstants.GuestRoleName) : LoggedOnUser.Roles.FirstOrDefault();
        }

        [ChildActionOnly]
        [OutputCache(Duration = AppConstants.DefaultCacheLengthInSeconds)]
        public PartialViewResult ListCategorySideMenu()
        {
            var catViewModel = new CategoryListViewModel { 
                AllPermissionSets = new Dictionary<Category, PermissionSet>(), 
                AllSubCategories = new Dictionary<Category, IEnumerable<Category>>() };

            using (UnitOfWorkManager.NewUnitOfWork())
            {
                foreach (var category in _categoryService.GetAllMainCategories())
                {
                    var permissionSet = RoleService.GetPermissions(category, UsersRole);
                    catViewModel.AllPermissionSets.Add(category, permissionSet);

                    var subCats = _categoryService.GetAllSubCategories(category.Id);
                    catViewModel.AllSubCategories.Add(category, subCats); 
                }
            }

            return PartialView(catViewModel);
        }

        public ActionResult Show(string slug, int? p)
        {
            using (UnitOfWorkManager.NewUnitOfWork())
            {
                // Get the category
                var category = _categoryService.Get(slug);

                // Set the page index
                var pageIndex = p ?? 1;

                // check the user has permission to this category
                var permissions = RoleService.GetPermissions(category, UsersRole);

                if (!permissions[AppConstants.PermissionDenyAccess].IsTicked)
                {

                    var topics = _topicService.GetPagedTopicsByCategory(pageIndex,
                                                                        SettingsService.GetSettings().TopicsPerPage,
                                                                        int.MaxValue, category.Id);

                    var isSubscribed = UserIsAuthenticated && (_categoryNotificationService.GetByUserAndCategory(LoggedOnUser, category).Any());

                    return View(new ViewCategoryViewModel
                                    {
                                        Permissions = permissions,
                                        Topics = topics,
                                        Category = category,
                                        PageIndex = pageIndex,
                                        TotalCount = topics.TotalCount,
                                        User = LoggedOnUser,
                                        IsSubscribed = isSubscribed
                                    });
                }

                return ErrorToHomePage(LocalizationService.GetResourceString("Errors.NoPermission"));
            }
        }

        [OutputCache(Duration = AppConstants.DefaultCacheLengthInSeconds)]
        public ActionResult CategoryRss(string slug)
        {
            using (UnitOfWorkManager.NewUnitOfWork())
            {


                // get an rss lit ready
                var rssTopics = new List<RssItem>();

                // Get the category
                var category = _categoryService.Get(slug);

                // check the user has permission to this category
                var permissions = RoleService.GetPermissions(category, UsersRole);

                if (!permissions[AppConstants.PermissionDenyAccess].IsTicked)
                {
                    var topics = _topicService.GetRssTopicsByCategory(AppConstants.ActiveTopicsListSize, category.Id);

                    rssTopics.AddRange(topics.Select(x =>
                                                         {
                                                             var firstOrDefault =
                                                                 x.Posts.FirstOrDefault(s => s.IsTopicStarter);
                                                             return firstOrDefault != null
                                                                        ? new RssItem
                                                                              {
                                                                                  Description = firstOrDefault.PostContent,
                                                                                  Link = x.NiceUrl,
                                                                                  Title = x.Name,
                                                                                  PublishedDate = x.CreateDate
                                                                              }
                                                                        : null;
                                                         }
                                           ));

                    return new RssResult(rssTopics, string.Format(LocalizationService.GetResourceString("Rss.Category.Title"), category.Name),
                                         string.Format(LocalizationService.GetResourceString("Rss.Category.Description"), category.Name));
                }

                return ErrorToHomePage(LocalizationService.GetResourceString("Errors.NothingToDisplay"));
            }
        }
    }
}
