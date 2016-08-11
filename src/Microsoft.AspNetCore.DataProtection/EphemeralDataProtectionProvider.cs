// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Cryptography.Cng;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.DataProtection
{
    /// <summary>
    /// An <see cref="IDataProtectionProvider"/> that is transient.
    /// </summary>
    /// <remarks>
    /// Payloads generated by a given <see cref="EphemeralDataProtectionProvider"/> instance can only
    /// be deciphered by that same instance. Once the instance is lost, all ciphertexts
    /// generated by that instance are permanently undecipherable.
    /// </remarks>
    public sealed class EphemeralDataProtectionProvider : IDataProtectionProvider
    {
        private readonly KeyRingBasedDataProtectionProvider _dataProtectionProvider;

        /// <summary>
        /// Creates an ephemeral <see cref="IDataProtectionProvider"/>, optionally providing
        /// services (such as logging) for consumption by the provider.
        /// </summary>
        public EphemeralDataProtectionProvider(ILoggerFactory loggerFactory)
        {
            IKeyRingProvider keyringProvider;
            if (OSVersionUtil.IsWindows())
            {
                // Fastest implementation: AES-256-GCM [CNG]
                keyringProvider = new EphemeralKeyRing<CngGcmAuthenticatedEncryptionSettings>();
            }
            else
            {
                // Slowest implementation: AES-256-CBC + HMACSHA256 [Managed]
                keyringProvider = new EphemeralKeyRing<ManagedAuthenticatedEncryptionSettings>();
            }

            var logger = loggerFactory.CreateLogger<EphemeralDataProtectionProvider>();
            logger?.UsingEphemeralDataProtectionProvider();

            _dataProtectionProvider = new KeyRingBasedDataProtectionProvider(keyringProvider, loggerFactory);
        }

        public IDataProtector CreateProtector(string purpose)
        {
            if (purpose == null)
            {
                throw new ArgumentNullException(nameof(purpose));
            }

            // just forward to the underlying provider
            return _dataProtectionProvider.CreateProtector(purpose);
        }

        private sealed class EphemeralKeyRing<T> : IKeyRing, IKeyRingProvider
            where T : IInternalAuthenticatedEncryptionSettings, new()
        {
            // Currently hardcoded to a 512-bit KDK.
            private const int NUM_BYTES_IN_KDK = 512 / 8;

            public IAuthenticatedEncryptor DefaultAuthenticatedEncryptor { get; } = new T().ToConfiguration(loggerFactory: null).CreateNewDescriptor().CreateEncryptorInstance();

            public Guid DefaultKeyId { get; } = default(Guid);

            public IAuthenticatedEncryptor GetAuthenticatedEncryptorByKeyId(Guid keyId, out bool isRevoked)
            {
                isRevoked = false;
                return (keyId == default(Guid)) ? DefaultAuthenticatedEncryptor : null;
            }

            public IKeyRing GetCurrentKeyRing()
            {
                return this;
            }
        }
    }
}
