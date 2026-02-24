using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;


namespace ECafe.Application.Services;

public abstract class BaseManager
{
    protected readonly IHttpContextAccessor HttpContextAccessor;
    protected readonly IMapper Mapper;
    protected readonly IConfiguration Configuration;

    protected BaseManager(IHttpContextAccessor httpContextAccessor, IMapper mapper, IConfiguration configuration)
    {
        HttpContextAccessor = httpContextAccessor;
        Mapper = mapper;
        Configuration = configuration;
    }

}

