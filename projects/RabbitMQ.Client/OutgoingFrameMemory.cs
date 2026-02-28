// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Diagnostics;
using static RabbitMQ.Client.Impl.Framing;

namespace RabbitMQ.Client
{
    internal struct OutgoingFrameMemory : IDisposable
    {
        private byte[]? _methodAndHeader;
        private readonly int _methodAndHeaderLength;
        private ReadOnlyMemory<byte> _body;
        private readonly int _maxBodyPayloadBytes;
        private readonly ushort _channelNumber;

        internal OutgoingFrameMemory(
            byte[] methodAndHeader,
            int methodAndHeaderLength)
        {
            _methodAndHeader = methodAndHeader;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = default;
            _channelNumber = 0;
            _maxBodyPayloadBytes = 0;
            Size = methodAndHeaderLength;
        }

        internal OutgoingFrameMemory(
            byte[] methodAndHeader,
            int methodAndHeaderLength,
            ReadOnlyMemory<byte> body,
            ushort channelNumber,
            int maxBodyPayloadBytes,
            int totalSize)
        {
            _methodAndHeader = methodAndHeader;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = body;
            _channelNumber = channelNumber;
            _maxBodyPayloadBytes = maxBodyPayloadBytes;
            Size = totalSize;
        }

        internal readonly int Size { get; }

        internal readonly void WriteTo(IBufferWriter<byte> writer)
        {
            Debug.Assert(_methodAndHeader is not null);
            ReadOnlySpan<byte> methodAndHeader = _methodAndHeader!.AsSpan(0, _methodAndHeaderLength);
            writer.Write(methodAndHeader);
            
            if (_body.Length == 0)
            {
                return;
            }

            ReadOnlySpan<byte> bodySpan = _body!.Span;
            int remainingBodyBytes = bodySpan.Length;
            int bodyOffset = 0;

            while (remainingBodyBytes > 0)
            {
                int payloadSize = remainingBodyBytes > _maxBodyPayloadBytes ? _maxBodyPayloadBytes : remainingBodyBytes;

                Span<byte> span = writer.GetSpan(BodySegment.HeaderSize + payloadSize + BodySegment.FooterSize);
                int offset = BodySegment.WriteTo(span, _channelNumber, bodySpan.Slice(bodyOffset, payloadSize));
                writer.Advance(offset);

                remainingBodyBytes -= payloadSize;
                bodyOffset += payloadSize;
            }
        }

        public void Dispose()
        {
            byte[]? methodAndHeader = _methodAndHeader;
            _methodAndHeader = null;
            if (methodAndHeader != null)
            {
                ArrayPool<byte>.Shared.Return(methodAndHeader);
                _methodAndHeader = default;
                _body = default;
            }
        }
    }
}
