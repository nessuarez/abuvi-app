export interface GitHubRelease {
  id: number
  tag_name: string
  name: string | null
  body: string | null
  published_at: string
  html_url: string
}
