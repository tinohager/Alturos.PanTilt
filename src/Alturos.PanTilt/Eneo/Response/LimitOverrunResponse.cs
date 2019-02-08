﻿namespace Alturos.PanTilt.Eneo.Response
{
    public class LimitOverrunResponse : BaseResponse
    {
        public LimitOverrunType LimitOverrunType { get; private set; }

        public LimitOverrunResponse(LimitOverrunType limitOverrunType, bool checksumValid) : base(ResponseType.LimitOverrun, checksumValid)
        {
            this.LimitOverrunType = limitOverrunType;
        }
    }
}
