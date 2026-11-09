mock_provider "azurerm" {}

variables {
  commit         = "0123456789abcdef0123456789abcdef01234567"
  operator_cidr  = "198.51.100.24/32"
  ssh_public_key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIAcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcH fixture-only"
}

run "secure_learning_topology" {
  command = plan

  assert {
    condition = length([for rule in azurerm_network_security_group.lab.security_rule : rule
    if rule.direction == "Inbound" && rule.access == "Allow"]) == 1
    error_message = "There must be exactly one inbound allow rule."
  }
  assert {
    condition = one([for rule in azurerm_network_security_group.lab.security_rule : rule
      if rule.access == "Allow"]).destination_port_range == "22" && one([
      for rule in azurerm_network_security_group.lab.security_rule : rule
    if rule.access == "Allow"]).source_address_prefix == var.operator_cidr
    error_message = "Only the specified operator may reach SSH; application ports must remain private."
  }
  assert {
    condition = one([for rule in azurerm_network_security_group.lab.security_rule : rule
    if rule.access == "Deny"]).priority == 200
    error_message = "Other inbound traffic must be denied before Azure's default VNet allow rule."
  }
  assert {
    condition = (azurerm_linux_virtual_machine.lab.disable_password_authentication &&
    azurerm_linux_virtual_machine.lab.admin_password == null)
    error_message = "The VM must use an existing SSH key and no admin password."
  }
  assert {
    condition = (azurerm_managed_disk.data.resource_group_name == azurerm_resource_group.data.name &&
      azurerm_resource_group.data.name != azurerm_resource_group.lab.name &&
      azurerm_managed_disk.data.public_network_access_enabled == false &&
    azurerm_managed_disk.data.network_access_policy == "DenyAll")
    error_message = "Retained data must live outside the compute group and cannot be publicly exported."
  }
  assert {
    condition     = azurerm_virtual_machine_data_disk_attachment.data.lun == 0 && azurerm_managed_disk.data.disk_size_gb == 100
    error_message = "Bootstrap requires the dedicated LUN0 data disk."
  }
  assert {
    condition = (azurerm_linux_virtual_machine.lab.location == "southeastasia" &&
    azurerm_linux_virtual_machine.lab.size == "Standard_D4s_v5")
    error_message = "The learning host must match the documented Singapore profile."
  }
}

run "reject_open_ssh" {
  command = plan
  variables { operator_cidr = "0.0.0.0/0" }
  expect_failures = [var.operator_cidr]
}

run "reject_ipv6" {
  command = plan
  variables { operator_cidr = "2001:db8::1/128" }
  expect_failures = [var.operator_cidr]
}

run "reject_network_range" {
  command = plan
  variables { operator_cidr = "198.51.100.24/24" }
  expect_failures = [var.operator_cidr]
}

run "reject_mutable_commit" {
  command = plan
  variables { commit = "main" }
  expect_failures = [var.commit]
}

run "reject_non_public_key" {
  command = plan
  variables { ssh_public_key = "private-key-material-is-not-a-public-key" }
  expect_failures = [var.ssh_public_key]
}

run "reject_too_small_data_disk" {
  command = plan
  variables { data_gib = 49 }
  expect_failures = [var.data_gib]
}
