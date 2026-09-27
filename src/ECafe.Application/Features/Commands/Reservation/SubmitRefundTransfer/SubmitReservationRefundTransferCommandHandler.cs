using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.File;
using ECafe.Application.Services.ReservationRefund.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Features.Commands.Reservation.SubmitRefundTransfer;

public sealed class SubmitReservationRefundTransferCommandHandler
    : IRequestHandler<SubmitReservationRefundTransferCommand, ReservationRefundTransferResponse>
{
    private readonly ISender _sender;
    private readonly IReservationRefundService _reservationRefundService;
    private readonly ILogger<SubmitReservationRefundTransferCommandHandler> _logger;

    public SubmitReservationRefundTransferCommandHandler(
        ISender sender,
        IReservationRefundService reservationRefundService,
        ILogger<SubmitReservationRefundTransferCommandHandler> logger)
    {
        _sender = sender;
        _reservationRefundService = reservationRefundService;
        _logger = logger;
    }

    public async Task<ReservationRefundTransferResponse> Handle(
        SubmitReservationRefundTransferCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ProofFile is null || request.ProofFile.Length == 0)
            throw new BadRequestException(ErrorCode.RefundTransferProofRequired);

        await _reservationRefundService.EnsureTransferCanBeSubmittedAsync(
            request.RestaurantId,
            request.RefundId,
            cancellationToken);

        var uploadedFile = await _sender.Send(new FileUploadCommand
        {
            File = request.ProofFile,
            FileTypeId = (int)FileTypeCode.PaymentReceipt
        }, cancellationToken);

        try
        {
            return await _reservationRefundService.SubmitTransferAsync(
                request.RestaurantId,
                request.RefundId,
                request.TransferReference,
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
                "Could not compensate refund transfer proof upload for file {FileId}. The unattached-file cleanup worker will retry it.",
                fileId);
        }
    }
}
