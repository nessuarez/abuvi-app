Please analyze and enrich the user story: $ARGUMENTS.

Follow these steps:

1. Read the task details from the provided source (Trello card URL, local file path, or inline description)
2. You will act as a product expert with technical knowledge
3. Understand the problem described in the task
4. Decide whether or not the User Story is completely detailed according to product's best practices: Include a full description of the functionality, a comprehensive list of fields to be updated, the structure and URLs of the necessary endpoints, the files to be modified according to the architecture and best practices, the steps required for the task to be considered complete, how to update any relevant documentation or create unit tests, and non-functional requirements related to security, performance, etc.
5. If the user story lacks the technical and specific detail necessary to allow the developer to be fully autonomous when completing it, provide an improved story that is clearer, more specific, and more concise in line with product best practices described in step 4. Use the technical context you will find in `ai-specs/specs/`. Return it in markdown format.
6. Save the enriched user story to `ai-specs/changes/[task_id]_enriched.md` so it can be referenced during implementation. If you don't have a task ID, save it as `ai-specs/changes/[feat|fix|refactor]-[task_short_name]/[task_name]_enriched.md`.
