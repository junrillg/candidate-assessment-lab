.DEFAULT_GOAL := help
.NOTPARALLEL:
PYTHON ?= python3

.PHONY: help configure validate up down logs status migrate seed sample test integration recovery verify diagrams backup restore deploy-init deploy-validate deploy-plan deploy-apply deploy-down aws-init aws-validate aws-plan aws-apply azure-init azure-validate azure-plan azure-apply
help:
	@echo 'Candidate Assessment — local operations'
	@echo '  make configure      Generate private local configuration (idempotent)'
	@echo '  make up             Build, start dependencies, migrate, seed, start app'
	@echo '  make down           Stop without deleting any data volumes'
	@echo '  make logs|status    Follow logs / show runtime state'
	@echo '  make migrate|seed   Explicit repeatable migration / fictional seed'
	@echo '  make sample         Create one synthetic attempt for inspection (AI processing, then human review)'
	@echo '  make test           Unit, architecture, adapter checks; serialized'
	@echo '  make integration    Real API/Postgres/Kafka checks against running local lab'
	@echo '  make recovery       Real provider-outage and interrupted-host recovery (local Ollama required)'
	@echo '  make verify         Tests, builds, config, Terraform and integration; serialized'
	@echo '  make diagrams       Export editorial figures and render Mermaid to SVG'
	@echo '  make backup         Private consistent database/recording/key backup'
	@echo '  make restore BACKUP=...  Restore a backup into an EMPTY stopped lab'
	@echo '  make deploy-init|deploy-validate|deploy-plan|deploy-apply|deploy-down'
	@echo '                      Terraform Docker-local profile only; no paid cloud'
	@echo '  make aws-init|aws-validate|aws-plan|aws-apply  AWS Singapore profile (paid if applied)'
	@echo '  make azure-init|azure-validate|azure-plan|azure-apply  Preferred Azure Singapore profile (paid if applied)'
	@echo '  Read docs/operations.md for credentials, retention, recovery and rollback.'

configure validate up down logs status migrate seed sample test integration recovery verify diagrams backup deploy-init deploy-validate deploy-plan deploy-apply deploy-down aws-init aws-validate aws-plan aws-apply azure-init azure-validate azure-plan azure-apply:
	$(PYTHON) scripts/lab.py $@

restore:
	$(PYTHON) scripts/lab.py restore --backup '$(BACKUP)'
