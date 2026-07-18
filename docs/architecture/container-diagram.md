# container diagram

```mermaid
flowchart TD
  User[Scholar/Mentor/Admin] --> Api[MindUnlocking.Api]
  Api --> App[MindUnlocking.Application]
  App --> Domain[MindUnlocking.Domain]
  Api --> Infra[MindUnlocking.Infrastructure]
  Worker[MindUnlocking.Workers] --> Infra
  Infra --> Azure[(Azure managed services)]
```
