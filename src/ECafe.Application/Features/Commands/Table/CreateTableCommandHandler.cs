using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class CreateTableCommandHandler : IRequestHandler<CreateTableCommand, int>
    {
        private readonly ITableService _tableService;

        public CreateTableCommandHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public async Task<int> Handle(CreateTableCommand request, CancellationToken cancellationToken)
        {
            return await _tableService.CreateAsync(request);
        }
    }
}
