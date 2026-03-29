using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Exceptions;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Auth.Concrete
{
    public class AuthManager : BaseManager, IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        public AuthManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           IUserRepository userRepository,
                           IJwtService jwtService)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
        }

        public async Task<string> LoginAsync(LoginRequestDto request)
        {
            if (request is null)
                throw new BusinessRuleException("request is not null!");

            var user = await  _userRepository.GetByEmailAsync(request.Email);

            if (user is null)
                throw new BusinessRuleException("User not found!");

            if (user.Password != request.Password)
                throw new BusinessRuleException("Password is wrong!");


            var token = _jwtService.GenerateToken(user);

            return token;
        }
    }
}
