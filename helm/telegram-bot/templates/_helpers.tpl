{{- define "telegram-bot.name" -}}
telegram-bot
{{- end }}

{{- define "telegram-bot.labels" -}}
app.kubernetes.io/name: {{ include "telegram-bot.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}
