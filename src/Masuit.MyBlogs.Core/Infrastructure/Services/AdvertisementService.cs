using CacheManager.Core;
using Masuit.LuceneEFCore.SearchEngine.Interfaces;
using Masuit.MyBlogs.Core.Infrastructure.Repository.Interface;
using Masuit.MyBlogs.Core.Infrastructure.Services.Interface;
using Masuit.MyBlogs.Core.Models.Entity;
using Masuit.MyBlogs.Core.Models.Enum;
using Masuit.Tools;
using Masuit.Tools.Linq;
using Masuit.Tools.RandomSelector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Masuit.MyBlogs.Core.Infrastructure.Services
{
    public partial class AdvertisementService : BaseService<Advertisement>, IAdvertisementService
    {
        public ICacheManager<List<Advertisement>> CacheManager { get; set; }

        public AdvertisementService(IBaseRepository<Advertisement> repository, ISearchEngine<DataContext> searchEngine, ILuceneIndexSearcher searcher) : base(repository, searchEngine, searcher)
        {
        }

        /// <summary>
        /// 按价格随机筛选一个元素
        /// </summary>
        /// <param name="type">广告类型</param>
        /// <param name="cid">分类id</param>
        /// <returns></returns>
        public Advertisement GetByWeightedPrice(AdvertiseType type, int? cid = null)
        {
            return GetsByWeightedPrice(1, type, cid).FirstOrDefault();
        }

        /// <summary>
        /// 按价格随机筛选一个元素
        /// </summary>
        /// <param name="count">数量</param>
        /// <param name="type">广告类型</param>
        /// <param name="cid">分类id</param>
        /// <returns></returns>
        public List<Advertisement> GetsByWeightedPrice(int count, AdvertiseType type, int? cid = null)
        {
            return CacheManager.GetOrAdd($"{type}:{count}-{cid}", _ =>
            {
                Expression<Func<Advertisement, bool>> where = a => a.Types.Contains(type.ToString("D")) && a.Status == Status.Available;
                if (cid.HasValue)
                {
                    var scid = cid.ToString();
                    if (Any(a => a.CategoryIds.Contains(scid)))
                    {
                        where = where.And(a => a.CategoryIds.Contains(scid) || string.IsNullOrEmpty(a.CategoryIds));
                    }
                }

                var list = GetQuery(where).AsEnumerable().Select(a => new WeightedItem<Advertisement>(a, a.CategoryIds is { Length: > 0 } ? (int)a.Price * 2 : (int)a.Price)).WeightedItems(count);
                var ids = list.Select(a => a.Id).ToArray();
                GetQuery(a => ids.Contains(a.Id)).UpdateFromQuery(a => new Advertisement()
                {
                    DisplayCount = a.DisplayCount + 1
                });

                return list;
            });
        }
    }
}