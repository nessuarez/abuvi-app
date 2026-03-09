const fs = require('fs');

async function main() {
  const args = process.argv.slice(2);
  const prDataPath = args[args.indexOf('--pr-data') + 1];
  const currentTag = args[args.indexOf('--current-tag') + 1];
  const previousTag = args[args.indexOf('--previous-tag') + 1];

  const prData = JSON.parse(fs.readFileSync(prDataPath, 'utf8'));
  const apiKey = process.env.ANTHROPIC_API_KEY;

  if (!apiKey) {
    console.error('Error: ANTHROPIC_API_KEY environment variable is not set.');
    process.exit(1);
  }

  if (prData.length === 0) {
    process.stdout.write(
      `No tracked pull requests found between \`${previousTag}\` and \`${currentTag}\`.`
    );
    return;
  }

  const prSummaries = prData
    .map((pr) => {
      const labels = (pr.labels || []).map((l) => l.name).join(', ');
      const body = (pr.body || 'No description provided.').substring(0, 500);
      return `- PR #${pr.number}: ${pr.title}\n  Author: ${pr.author?.login || 'unknown'}\n  Labels: ${labels || 'none'}\n  Description: ${body}`;
    })
    .join('\n\n');

  const prompt = `You are generating a changelog for release ${currentTag} of ABUVI, a web platform for a summer camp association. The project is a monorepo with a .NET 9 backend and Vue 3 frontend.

Previous release: ${previousTag}
Current release: ${currentTag}
Total PRs: ${prData.length}

Here are the pull requests merged in this release:

${prSummaries}

Generate a human-readable changelog in Markdown following these rules:
1. Write in English
2. Group changes into these categories (omit empty ones): "New Features", "Improvements", "Bug Fixes", "Internal"
3. Each item should be a single clear sentence describing the user-facing impact, not technical implementation details
4. Use the format: "- Brief description (#PR_NUMBER)"
5. Start with a one-sentence release summary
6. Do NOT include a title/header with the version number (the GitHub Release already shows that)
7. Keep it concise — max 2-3 sentences per item
8. PR descriptions may be in Spanish — translate the meaning to English for the changelog`;

  const response = await fetch('https://api.anthropic.com/v1/messages', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-api-key': apiKey,
      'anthropic-version': '2023-06-01',
    },
    body: JSON.stringify({
      model: 'claude-sonnet-4-6',
      max_tokens: 1024,
      messages: [{ role: 'user', content: prompt }],
    }),
  });

  if (!response.ok) {
    const errorBody = await response.text();
    console.error(`Claude API error (${response.status}): ${errorBody}`);
    process.exit(1);
  }

  const result = await response.json();
  const changelog = result.content[0].text;

  const output = `${changelog}\n\n---\n*Changelog generated with Claude AI from ${prData.length} merged PR(s).*`;

  process.stdout.write(output);
}

main().catch((err) => {
  console.error('Unexpected error:', err.message);
  process.exit(1);
});
