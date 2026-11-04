using AutoMapper;
using EasyOrderWhatsAppBLL.ViewModels.TenantViewModels;
using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppBLL.Profiles
{
    public class TenantProfile : Profile
    {
        public TenantProfile()
        {
            CreateMap<Tenant, TenantViewModel>();
            CreateMap<RegisterTenantViewModel, Tenant>()
                .ForMember(dest => dest.StoreKey, opt => opt.MapFrom(src => src.StoreKey.Trim().ToLower()));
        }
    }
}
