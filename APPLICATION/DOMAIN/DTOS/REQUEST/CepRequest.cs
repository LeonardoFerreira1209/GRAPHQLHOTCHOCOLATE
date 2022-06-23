﻿namespace APPLICATION.DOMAIN.DTOS.REQUEST;

/// <summary>
/// Request de Cep
/// </summary>
public class CepRequest
{
    /// <summary>
    /// Código de endereçamento postal. 
    /// </summary>
    public long Cep { get; set; }
}
