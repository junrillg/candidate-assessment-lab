terraform {
  required_version = ">= 1.7, < 2.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

provider "azurerm" {
  features {}
  resource_provider_registrations = "none"
}

variable "commit" {
  type        = string
  description = "Published full 40-character Git commit to deploy."
  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.commit))
    error_message = "Set commit to an existing published full Git SHA."
  }
}

variable "operator_cidr" {
  type        = string
  description = "The operator's public IPv4 address in canonical /32 form; only this address may SSH."
  validation {
    condition = try(
      cidrnetmask(var.operator_cidr) != "" &&
      var.operator_cidr == "${cidrhost(var.operator_cidr, 0)}/32",
      false
    )
    error_message = "operator_cidr must be one canonical IPv4 /32, never an open network or IPv6 range."
  }
}

variable "ssh_public_key" {
  type        = string
  description = "Existing OpenSSH RSA >=2048-bit or Ed25519 public key; never a private key."
  validation {
    condition     = can(regex("^(ssh-ed25519|ssh-rsa) [A-Za-z0-9+/]+={0,2}( [^\r\n]+)?$", var.ssh_public_key))
    error_message = "Supply a single-line OpenSSH public key. The provider also validates the key encoding and RSA size."
  }
}

variable "data_gib" {
  type        = number
  default     = 100
  description = "Retained Docker, recording, configuration and model disk size in GiB."
  validation {
    condition     = var.data_gib >= 50 && var.data_gib <= 500 && floor(var.data_gib) == var.data_gib
    error_message = "data_gib must be an integer from 50 to 500."
  }
}

locals {
  name     = "candidate-assessment-lab"
  location = "southeastasia"
  tags     = { Project = "candidate-assessment-lab", Purpose = "learning", ManagedBy = "terraform" }
}

resource "azurerm_resource_group" "lab" {
  name     = local.name
  location = local.location
  tags     = local.tags
}

resource "azurerm_resource_group" "data" {
  name     = "${local.name}-data"
  location = local.location
  tags     = local.tags
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_virtual_network" "lab" {
  name                = local.name
  resource_group_name = azurerm_resource_group.lab.name
  location            = local.location
  address_space       = ["10.75.0.0/24"]
  tags                = local.tags
}

resource "azurerm_subnet" "lab" {
  name                 = "application-host"
  resource_group_name  = azurerm_resource_group.lab.name
  virtual_network_name = azurerm_virtual_network.lab.name
  address_prefixes     = ["10.75.0.0/26"]
}

resource "azurerm_network_security_group" "lab" {
  name                = local.name
  location            = local.location
  resource_group_name = azurerm_resource_group.lab.name
  tags                = local.tags
  security_rule {
    name                       = "OperatorSSH"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "22"
    source_address_prefix      = var.operator_cidr
    destination_address_prefix = "*"
  }
  security_rule {
    name                       = "DenyOtherInbound"
    priority                   = 200
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }
}

resource "azurerm_subnet_network_security_group_association" "lab" {
  subnet_id                 = azurerm_subnet.lab.id
  network_security_group_id = azurerm_network_security_group.lab.id
}

resource "azurerm_public_ip" "lab" {
  name                = local.name
  location            = local.location
  resource_group_name = azurerm_resource_group.lab.name
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = local.tags
}

resource "azurerm_network_interface" "lab" {
  name                = local.name
  location            = local.location
  resource_group_name = azurerm_resource_group.lab.name
  tags                = local.tags
  ip_configuration {
    name                          = "host"
    subnet_id                     = azurerm_subnet.lab.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.lab.id
  }
}

resource "azurerm_managed_disk" "data" {
  name                          = "${local.name}-data"
  location                      = local.location
  resource_group_name           = azurerm_resource_group.data.name
  storage_account_type          = "StandardSSD_LRS"
  create_option                 = "Empty"
  disk_size_gb                  = var.data_gib
  public_network_access_enabled = false
  network_access_policy         = "DenyAll"
  tags                          = local.tags
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_linux_virtual_machine" "lab" {
  name                            = local.name
  location                        = local.location
  resource_group_name             = azurerm_resource_group.lab.name
  size                            = "Standard_D4s_v5"
  admin_username                  = "azureuser"
  disable_password_authentication = true
  network_interface_ids           = [azurerm_network_interface.lab.id]
  custom_data                     = base64encode(templatefile("${path.module}/cloud-init.sh.tftpl", { commit = var.commit }))
  tags                            = local.tags
  admin_ssh_key {
    username   = "azureuser"
    public_key = var.ssh_public_key
  }
  os_disk {
    name                 = "${local.name}-os"
    caching              = "ReadWrite"
    storage_account_type = "StandardSSD_LRS"
    disk_size_gb         = 32
  }
  source_image_reference {
    publisher = "Canonical"
    offer     = "ubuntu-24_04-lts"
    sku       = "server"
    version   = "latest"
  }
  depends_on = [azurerm_subnet_network_security_group_association.lab]
}

resource "azurerm_virtual_machine_data_disk_attachment" "data" {
  managed_disk_id    = azurerm_managed_disk.data.id
  virtual_machine_id = azurerm_linux_virtual_machine.lab.id
  lun                = 0
  caching            = "None"
}

output "vm_id" {
  value = azurerm_linux_virtual_machine.lab.id
}

output "public_ip" {
  value = azurerm_public_ip.lab.ip_address
}

output "retained_data_disk_id" {
  value = azurerm_managed_disk.data.id
}

output "retained_data_resource_group" {
  value       = azurerm_resource_group.data.name
  description = "Retain this group as well as the disk during teardown; deleting a resource group deletes its resources."
}

output "ssh_command" {
  value = "ssh azureuser@${azurerm_public_ip.lab.ip_address}"
}

output "forward_command" {
  value = "ssh -N -L 127.0.0.1:5188:127.0.0.1:5188 azureuser@${azurerm_public_ip.lab.ip_address}"
}
