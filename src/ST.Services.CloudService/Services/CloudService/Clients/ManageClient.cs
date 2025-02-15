﻿using System.Application.Models;
using System.Application.Services.CloudService.Clients.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;

namespace System.Application.Services.CloudService.Clients
{
    internal sealed class ManageClient : ApiClient, IManageClient
    {
        public ManageClient(IApiConnection conn) : base(conn)
        {
        }

        public Task<IApiResponse<string>> ChangeBindPhoneNumber(ChangePhoneNumberRequest.Validation request)
            => conn.SendAsync<ChangePhoneNumberRequest.Validation, string>(
                isAnonymous: false,
                isSecurity: true,
                method: HttpMethod.Post,
                requestUri: "api/Manage/ChangeBindPhoneNumber",
                request: request,
                cancellationToken: default,
                responseContentMaybeNull: false);

        public Task<IApiResponse> ChangeBindPhoneNumber(ChangePhoneNumberRequest.New request)
            => conn.SendAsync(
                isAnonymous: false,
                isSecurity: true,
                method: HttpMethod.Put,
                requestUri: "api/Manage/ChangeBindPhoneNumber",
                request: request,
                cancellationToken: default);

        public Task<IApiResponse> DeleteAccount()
        {
            throw new NotImplementedException();
        }

        public Task<IApiResponse<ClockInResponse>> ClockIn()
            => conn.SendAsync<ClockInRequest, ClockInResponse>(
                isAnonymous: true,
                isSecurity: true,
                method: HttpMethod.Post,
                requestUri: "api/Manage/ClockIn",
                request: new ClockInRequest
                {
                    CreationTime = DateTimeOffset.Now,
                    //ClientDeviceId = "",
                },
                cancellationToken: default);

        public Task<IApiResponse> EditUserProfile(EditUserProfileRequest request)
            => conn.SendAsync(
                isAnonymous: false,
                isSecurity: true,
                method: HttpMethod.Post,
                requestUri: "api/Manage/EditUserProfile",
                request: request,
                cancellationToken: default);
    }
}