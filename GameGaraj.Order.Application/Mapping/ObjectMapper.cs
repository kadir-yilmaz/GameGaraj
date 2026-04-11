using AutoMapper;

namespace GameGaraj.Order.Application.Mapping
{
    /// <summary>
    /// Lazy singleton pattern ile AutoMapper instance'ı
    /// </summary>
    public static class ObjectMapper
    {
        private static readonly Lazy<IMapper> lazy = new Lazy<IMapper>(() =>
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            return config.CreateMapper();
        });

        public static IMapper Mapper => lazy.Value;
    }
}
