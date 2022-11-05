﻿using APPLICATION.DOMAIN.DTOS.RESPONSE;
using APPLICATION.ENUMS;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TOOLS.API.CONTROLLER.config;

[Route("api/[controller]")][ApiController]
public class ConfigController : ControllerBase
{
    [HttpOptions("options")]
    public async Task<ApiResponse<object>> Options()
    {
        return await Task.FromResult(new ApiResponse<object>(true, StatusCodes.SuccessOK, new List<DadosNotificacao> { new DadosNotificacao(StatusCodes.SuccessOK, "Headers suportados pela aplicação.") }));
    }
}
