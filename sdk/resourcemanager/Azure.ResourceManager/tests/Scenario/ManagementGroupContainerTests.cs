﻿using System.Threading.Tasks;
using Azure.Core.TestFramework;
using Azure.ResourceManager.Management;
using Azure.ResourceManager.Management.Models;
using NUnit.Framework;

namespace Azure.ResourceManager.Tests
{
    public class ManagementGroupContainerTests : ResourceManagerTestBase
    {
        private ManagementGroup _mgmtGroup;
        private string _mgmtGroupName;

        public ManagementGroupContainerTests(bool isAsync)
        : base(isAsync)//, RecordedTestMode.Record)
        {
        }

        [OneTimeSetUp]
        public async Task GetGlobalManagementGroup()
        {
            _mgmtGroupName = SessionRecording.GenerateAssetName("mgmt-group-");
            _mgmtGroup = await GlobalClient.GetManagementGroups().CreateOrUpdateAsync(_mgmtGroupName, new CreateManagementGroupOptions());
            _mgmtGroup = await _mgmtGroup.GetAsync();
            StopSessionRecording();
        }

        [RecordedTest]
        public async Task List()
        {
            var mgmtGroupContainer = Client.GetManagementGroups();
            ManagementGroupInfo mgmtGroup = null;
            await foreach(var item in mgmtGroupContainer.GetAllAsync("no-cache"))
            {
                mgmtGroup = item;
                break;
            }
            Assert.IsNotNull(mgmtGroup, "No management groups found in list");
            Assert.IsNotNull(mgmtGroup.Data.DisplayName, "DisplayName was null");
            Assert.IsNotNull(mgmtGroup.Data.Id, "Id was null");
            Assert.IsNotNull(mgmtGroup.Data.Name, "Name was null");
            Assert.IsNotNull(mgmtGroup.Data.TenantId, "TenantId was null");
            Assert.IsNotNull(mgmtGroup.Data.Type, "Type was null");
        }

        [RecordedTest]
        public async Task Get()
        {
            ManagementGroup mgmtGroup = await Client.GetManagementGroups().GetAsync(_mgmtGroupName, cacheControl: "no-cache");
            CompareMgmtGroups(_mgmtGroup, mgmtGroup);
            RequestFailedException ex = Assert.ThrowsAsync<RequestFailedException>(async () => _ = await Client.GetManagementGroups().GetAsync("NotThere", cacheControl: "no-cache"));
            Assert.AreEqual(403, ex.Status);
        }

        [RecordedTest]
        public async Task TryGet()
        {
            ManagementGroup mgmtGroup = await Client.GetManagementGroups().GetIfExistsAsync(_mgmtGroup.Data.Name, cacheControl: "no-cache");
            CompareMgmtGroups(_mgmtGroup, mgmtGroup);
            var ex = Assert.ThrowsAsync<RequestFailedException>(async () => _ = await Client.GetManagementGroups().GetAsync("NotThere", cacheControl: "no-cache"));
            Assert.AreEqual(403, ex.Status);
        }

        [RecordedTest]
        public async Task CheckIfExists()
        {
            Assert.IsTrue(await Client.GetManagementGroups().CheckIfExistsAsync(_mgmtGroup.Data.Name, cacheControl: "no-cache"));
            var ex = Assert.ThrowsAsync<RequestFailedException>(async () => await Client.GetManagementGroups().CheckIfExistsAsync(_mgmtGroup.Data.Name + "x", cacheControl: "no-cache"));
            Assert.AreEqual(403, ex.Status); //you get forbidden vs not found here for some reason by the service
        }

        [RecordedTest]
        public async Task CreateOrUpdate()
        {
            string mgmtGroupName = Recording.GenerateAssetName("mgmt-group-");
            ManagementGroup mgmtGroup = await Client.GetManagementGroups().CreateOrUpdateAsync(mgmtGroupName, new CreateManagementGroupOptions());
            Assert.AreEqual($"/providers/Microsoft.Management/managementGroups/{mgmtGroupName}", mgmtGroup.Data.Id.ToString());
            Assert.AreEqual(mgmtGroupName, mgmtGroup.Data.Name);
            Assert.AreEqual(mgmtGroupName, mgmtGroup.Data.DisplayName);
            Assert.AreEqual(ManagementGroupOperations.ResourceType, mgmtGroup.Data.Type);
        }

        [RecordedTest]
        public async Task StartCreateOrUpdate()
        {
            string mgmtGroupName = Recording.GenerateAssetName("mgmt-group-");
            var mgmtGroupOp = await Client.GetManagementGroups().StartCreateOrUpdateAsync(mgmtGroupName, new CreateManagementGroupOptions());
            ManagementGroup mgmtGroup = await mgmtGroupOp.WaitForCompletionAsync();
            Assert.AreEqual($"/providers/Microsoft.Management/managementGroups/{mgmtGroupName}", mgmtGroup.Data.Id.ToString());
            Assert.AreEqual(mgmtGroupName, mgmtGroup.Data.Name);
            Assert.AreEqual(mgmtGroupName, mgmtGroup.Data.DisplayName);
            Assert.AreEqual(ManagementGroupOperations.ResourceType, mgmtGroup.Data.Type);
        }

        [RecordedTest]
        public async Task CheckNameAvailability()
        {
            var rq = new CheckNameAvailabilityOptions();
            rq.Name = "this-should-not-exist";
            var rs = await Client.GetManagementGroups().CheckNameAvailabilityAsync(rq);
            Assert.IsTrue(rs.Value.NameAvailable);
        }
    }
}
