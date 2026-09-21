using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.Commands.File;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Features.Commands.Reservation.SubmitPaymentProof;

public sealed class SubmitPaymentProofCommandHandler
    : IRequestHandler<SubmitPaymentProofCommand, PaymentProofResponse>
{
    private readonly ISender _sender;
    private readonly IReservationService _reservationService;
    private readonly ILogger<SubmitPaymentProofCommandHandler> _logger;

    public SubmitPaymentProofCommandHandler(
        ISender sender,
        IReservationService reservationService,
        ILogger<SubmitPaymentProofCommandHandler> logger)
    {
        _sender = sender;
        _reservationService = reservationService;
        _logger = logger;
    }

    public async Task<PaymentProofResponse> Handle(
        SubmitPaymentProofCommand request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            throw new BadRequestException("Ödəniş çekini seçin.");

        await _reservationService.EnsurePaymentProofCanBeSubmittedAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);

        var uploadedFile = await _sender.Send(new FileUploadCommand
        {
            File = request.File,
            FileTypeId = (int)FileTypeCode.PaymentReceipt
        }, cancellationToken);

        try
        {
            return await _reservationService.SubmitPaymentProofAsync(
                request.RestaurantId,
                request.ReservationId,
                uploadedFile.Id,
                cancellationToken);
        }
        catch
        {
            await TryDeleteUnattachedFileAsync(uploadedFile.Id);
            throw;
        }
    }

    private async Task TryDeleteUnattachedFileAsync(int fileId)
    {
        try
        {
            await _sender.Send(new DeleteFileCommand { FileId = fileId });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not compensate payment proof upload for file {FileId}. The unattached-file cleanup worker will retry it.",
                fileId);
        }
    }
}
