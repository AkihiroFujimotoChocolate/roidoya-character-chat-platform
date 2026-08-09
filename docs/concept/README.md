# Roidoya Character Chat Platform Concept

## About This Document

This document defines the high-level concept of the Roidoya Character Chat Platform (RCCP).

The purpose, goals, design principles, and non-goals described here form a stable foundation for RCCP and are expected to change only when there is a strong reason to reconsider the direction of the platform.

This document describes what RCCP is intended to become. It does not describe the feature set or maturity of the current implementation.

Detailed roles, role-specific value, system architecture, APIs, data models, protocols, deployment models, cloud services, and specific implementation techniques are defined separately and may evolve as RCCP develops.

## Purpose

The core purpose of RCCP is:

> **RCCP is a platform that enables developers to build, operate, validate, and continuously improve reliable and maintainable character chat services that support content creators, system operators, and end users, while reusing common technical foundations.**

RCCP aims to reduce the need to repeatedly solve the same design, implementation, integration, and operational problems from scratch for each character chat service.

RCCP is not intended merely to reduce development effort. The reusable foundation should ultimately support the creation, ongoing operation, and continued improvement of character experiences.

## Goals

RCCP aims to provide a foundation for character services that can:

- Deliver consistent character experiences that reflect the intent of their creators.
- Remain reliable and maintainable through ongoing operation.
- Support the different responsibilities involved in development, content creation, system operation, and end-user experience without unnecessarily coupling them.
- Reuse common technical foundations while preserving flexibility for service-specific requirements and new technologies.
- Evolve over time without unnecessarily disrupting existing functionality, integrations, usage patterns, or character experiences.
- Support validation and continuous improvement throughout the lifetime of a service.
- Allow character experiences to extend beyond free-form conversation when a service requires other forms of interaction.

The current focus of RCCP is text-based conversation. These goals do not require every service to provide the same features or the same form of character experience.

## Design Principles

The following principles guide RCCP's more detailed architecture, specifications, and implementation. They define long-term design direction without fixing specific service boundaries, protocols, products, or deployment technologies.

### Keep Responsibilities and Boundaries Clear

In the RCCP backend, major responsibilities are conceptually organized into **Channel Adapters, Orchestration, and Core Services**.

- **Channel Adapters** handle channel-specific concerns such as input and output, authentication, events, identifiers, and external constraints.
- **Orchestration** combines capabilities into processing flows and character experiences.
- **Core Services** provide reusable backend capabilities such as character behavior, memory, state, model access, search, and external functions.

RCCP should keep responsibilities and contracts between these areas clear so that changes in one area do not unnecessarily propagate into unrelated areas. Concrete service boundaries, deployment units, and communication mechanisms are defined separately.

### Treat Orchestration as a Primary Boundary for Experience Design

Orchestration is a primary boundary through which character-specific experiences can be designed and changed.

RCCP should provide reusable samples and templates for common character experiences. Common or relatively static experiences may be made easier to work with through forms, templates, previews, or similar interfaces, while advanced customization should allow the workflow itself to be edited.

The exact authoring interface, permission model, workflow representation, and execution platform are defined separately.

### Make Continuity Reusable Without Requiring a Single Model

RCCP should support character experiences in which past interactions, memory, relationships, state, events, and other information can influence later interactions.

Reusable patterns, samples, and templates should make common forms of continuity easier to build by combining orchestration with memory, state, relationship, and related capabilities.

RCCP should not require every service to use a single memory, relationship, or state model. These mechanisms should remain selectable, replaceable, and extensible according to the needs of each character and service.

### Prefer Existing Mechanisms Over Unnecessary Reinvention

When established open-source software, cloud services, communication formats, or general software-design approaches adequately solve a problem, RCCP should prefer using them over introducing an RCCP-specific mechanism without a clear need.

The same principle applies to service-to-service message representations. Where established concepts used by chat and model-based systems are sufficient, RCCP should reuse them rather than inventing unnecessary platform-specific formats.

RCCP should also avoid making a particular model provider's API a platform-wide dependency.

### Treat Stable Interfaces and Contracts as Long-Lived

Message formats, APIs, and other interfaces between major RCCP responsibilities should be treated as stable contracts that may remain in use for a long time.

Compatible additions should preserve existing use where practical. When a breaking change is necessary, RCCP should allow staged migration and, where appropriate, coexistence of old and new contract versions rather than requiring every dependent component to change immediately.

The concrete versioning mechanism is defined separately for each type of interface or transport.

### Isolate the Impact of Lower-Level Technology Changes

Changes to models, external services, channels, cloud platforms, infrastructure, and other lower-level technologies should not unnecessarily propagate into unrelated responsibilities, usage patterns, or character experiences.

In particular, changes made for infrastructure or operational reasons should not require unrelated changes to how character experiences are created or operated, or to the experiences delivered to end users.

RCCP may use cloud-specific or technology-specific mechanisms where they are useful. Their dependencies should be localized to the implementation boundaries that actually require them.

### Prefer Established Terminology

RCCP should use established terminology from software engineering, system design, and related technical fields whenever those terms accurately express the intended concept.

RCCP-specific terminology should be introduced only when existing terminology cannot express an RCCP-specific concept clearly enough.

## Non-Goals

RCCP does not treat the following as primary goals:

- Making one-click character chat creation for anyone a primary value proposition.
- Fully automating creative judgment involved in character or content creation.
- Automatically generating high-quality characters or character experiences as a core responsibility of RCCP.
- Requiring every character service to follow a single experience, continuity, or relationship model.
- Making LINE, Azure, or any other specific channel, cloud platform, model provider, external service, or existing implementation a platform-wide requirement.
- Requiring services to adopt RCCP-specific abstractions when doing so would unnecessarily prevent service-specific design choices or the adoption of new technologies.

These non-goals do not mean that simple setup, graphical administration, low-code or no-code tools, automation, or integrations with specific technologies are excluded from RCCP.

RCCP is intended to provide reusable orchestration samples and templates, as well as reusable patterns for building continuity across character interactions. The specific user interfaces, formats, and implementation mechanisms used to provide them are defined separately.

## Areas Defined in More Detail Later

This high-level concept intentionally leaves several areas to more detailed concept, architecture, and specification work, including:

- Detailed definitions of the roles involved in RCCP-based services.
- The value RCCP aims to provide to developers, system operators, content creators, and end users.
- The scope of the RCCP core and the boundary between standard and extensible functionality.
- The boundary between RCCP, external services, and character-specific content.
- Concrete service boundaries, communication mechanisms, implementation technologies, and deployment models for Channel Adapters, Orchestration, Core Services, and other architectural areas.
- The workflow execution platform, authoring interfaces, permissions, and the representation, distribution, and versioning of samples and templates.
- Concrete message schemas and compatibility, versioning, deprecation, and migration policies for APIs, events, and other stable contracts.
- Administration and authoring interfaces, including graphical, low-code, or no-code approaches.
- Multi-tenancy and other deployment or service-organization models.
- Concrete memory, relationship, state, event, and other mechanisms used to implement continuity.
- Future interaction modes beyond the current focus on text-based conversation.
