using AutoMapper;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppBLL.Profiles
{
    public class OrderProfile : Profile
    {
        public OrderProfile()
        {
            CreateMap<CreateOrderViewModel, Order>().ReverseMap();

            CreateMap<Order, OrderViewModel>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<OrderViewModel, Order>();
        }
    }
}
