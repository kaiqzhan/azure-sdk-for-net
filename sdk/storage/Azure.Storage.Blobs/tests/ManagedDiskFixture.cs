﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.TestFramework;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.Storage.Test;

namespace Azure.Storage.Blobs.Tests.ManagedDisk
{
    /// <summary>
    /// This fixture makes sure that managed disk snapshots are initialized and destroyed once per whole test suite.
    ///
    /// Deleting snapshots at test class level was not viable as it led to race conditions related to access rights,
    /// i.e. if one of the middle (or first) snapshots is deleted it seems that service revokes read access while it squashes data.
    /// </summary>
    public class ManagedDiskFixture : SetUpFixtureBase<BlobTestEnvironment>
    {
        public ManagedDiskFixture()
            : base(/* RecordedTestMode.Live /* to hardocode the mode */)
        {
        }

        public static ManagedDiskFixture Instance { get; private set; }

        private ManagedDiskConfiguration _config;
        private ComputeManagementClient _computeClient;
        private Snapshot _snapshot1;
        private Snapshot _snapshot2;

        public Uri Snapshot1SASUri { get; private set; }
        public Uri Snapshot2SASUri { get; private set; }

        public override async Task SetUp()
        {
            if (Environment.Mode != RecordedTestMode.Playback)
            {
                _config = TestConfigurations.DefaultTargetManagedDisk;

                TokenCredential tokenCredentials = new Identity.ClientSecretCredential(
                    _config.ActiveDirectoryTenantId, _config.ActiveDirectoryApplicationId, _config.ActiveDirectoryApplicationSecret);
                _computeClient = new ComputeManagementClient(_config.SubsriptionId, tokenCredentials);

                var disks = await _computeClient.Disks.ListByResourceGroupAsync(_config.ResourceGroupName).ToListAsync();
                var disk = disks.Where(d => d.Name.Contains(_config.DiskNamePrefix)).First();

                _snapshot1 = await CreateSnapshot(disk, _config.DiskNamePrefix + Guid.NewGuid().ToString().Replace("-", ""));

                // The disk is attached to VM, wait some time to let OS background jobs write something to disk to create delta.
                await Task.Delay(TimeSpan.FromSeconds(60));

                _snapshot2 = await CreateSnapshot(disk, _config.DiskNamePrefix + Guid.NewGuid().ToString().Replace("-", ""));

                Snapshot1SASUri = await GrantAccess(_snapshot1);
                Snapshot2SASUri = await GrantAccess(_snapshot2);
            }

            Instance = this;
        }

        public override async Task TearDown()
        {
            if (Environment.Mode != RecordedTestMode.Playback)
            {
                await RevokeAccess(_snapshot1);
                await RevokeAccess(_snapshot2);

                await DeleteSnapshot(_snapshot1);
                await DeleteSnapshot(_snapshot2);
            }
        }

        private async Task<Snapshot> CreateSnapshot(Disk disk, string name)
        {
            var snapshotCreateOperation = await _computeClient.Snapshots.StartCreateOrUpdateAsync(_config.ResourceGroupName, name,
                new Snapshot(_config.Location)
                {
                    CreationData = new CreationData(DiskCreateOption.Copy)
                    {
                        SourceResourceId = disk.Id
                    },
                    Incremental = true,
                });
            return await snapshotCreateOperation.WaitForCompletionAsync();
        }

        private async Task DeleteSnapshot(Snapshot snapshot)
        {
            var snapshotDeleteOperation = await _computeClient.Snapshots.StartDeleteAsync(_config.ResourceGroupName, snapshot.Name);
            await snapshotDeleteOperation.WaitForCompletionAsync();
        }

        private async Task<Uri> GrantAccess(Snapshot snapshot)
        {
            var grantOperation = await _computeClient.Snapshots.StartGrantAccessAsync(_config.ResourceGroupName, snapshot.Name,
                new GrantAccessData(AccessLevel.Read, 3600));
            AccessUri accessUri = await grantOperation.WaitForCompletionAsync();
            return new Uri(accessUri.AccessSAS);
        }

        private async Task RevokeAccess(Snapshot snapshot)
        {
            var revokeOperation = await _computeClient.Snapshots.StartRevokeAccessAsync(_config.ResourceGroupName, snapshot.Name);
            await revokeOperation.WaitForCompletionAsync();
        }
    }
}
