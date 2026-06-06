import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { hubUrl } from '@/env'
import { queryKeys } from '@/api/query-keys'
import {
  agentUsageSchema,
  linkedDocumentSchema,
  promptSchema,
  taskSummarySchema,
  workspaceFileChangedSchema,
} from '@/api/schemas'
import { fileKey, parentDirectoryPath } from '@/features/files/file-key'

type FileSubscription = {
  workingDirectoryId: string
  relativePath: string
  count: number
}

type PromptHubContextValue = {
  connected: boolean
  joinWorkingDirectory: (id: string) => void
  leaveWorkingDirectory: (id: string) => void
  joinTasks: () => void
  leaveTasks: () => void
  joinFile: (workingDirectoryId: string, relativePath: string) => void
  leaveFile: (workingDirectoryId: string, relativePath: string) => void
}

const PromptHubContext = createContext<PromptHubContextValue | null>(null)

export function PromptHubProvider({ children }: { children: React.ReactNode }) {
  const queryClient = useQueryClient()
  const connectionRef = useRef<HubConnection | null>(null)
  const joinedWorkingDirectoriesRef = useRef(new Set<string>())
  const joinedFilesRef = useRef(new Map<string, FileSubscription>())
  const tasksJoinedRef = useRef(false)
  const [connected, setConnected] = useState(false)

  const invokeJoin = useCallback((id: string) => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('JoinWorkingDirectory', id)
    }
  }, [])

  const invokeLeave = useCallback((id: string) => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveWorkingDirectory', id)
    }
  }, [])

  const invokeJoinTasks = useCallback(() => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('JoinTasks')
    }
  }, [])

  const invokeJoinFile = useCallback((workingDirectoryId: string, relativePath: string) => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('JoinFile', workingDirectoryId, relativePath)
    }
  }, [])

  const invokeLeaveFile = useCallback((workingDirectoryId: string, relativePath: string) => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveFile', workingDirectoryId, relativePath)
    }
  }, [])

  const rejoinAll = useCallback(() => {
    joinedWorkingDirectoriesRef.current.forEach(invokeJoin)
    if (tasksJoinedRef.current) {
      invokeJoinTasks()
    }
    joinedFilesRef.current.forEach(({ workingDirectoryId, relativePath }) => {
      invokeJoinFile(workingDirectoryId, relativePath)
    })
  }, [invokeJoin, invokeJoinFile, invokeJoinTasks])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build()

    connectionRef.current = connection

    connection.on('PromptCreated', (payload: unknown) => {
      const prompt = promptSchema.parse(payload)
      queryClient.setQueryData(queryKeys.prompts.detail(prompt.id), prompt)
      queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    })

    connection.on('PromptUpdated', (payload: unknown) => {
      const prompt = promptSchema.parse(payload)
      queryClient.setQueryData(queryKeys.prompts.detail(prompt.id), prompt)
      queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
      queryClient.invalidateQueries({ queryKey: queryKeys.prompts.versions(prompt.id) })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    })

    connection.on('PromptDeleted', (promptId: string) => {
      queryClient.removeQueries({ queryKey: queryKeys.prompts.detail(promptId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    })

    connection.on('LinkedDocumentLinked', (payload: unknown) => {
      const document = linkedDocumentSchema.parse(payload)
      queryClient.setQueryData(queryKeys.linkedDocuments.detail(document.id), document)
      queryClient.invalidateQueries({ queryKey: queryKeys.linkedDocuments.forPrompt(document.promptId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    })

    connection.on('LinkedDocumentUpdated', (payload: unknown) => {
      const document = linkedDocumentSchema.parse(payload)
      queryClient.setQueryData(queryKeys.linkedDocuments.detail(document.id), document)
      queryClient.invalidateQueries({ queryKey: queryKeys.linkedDocuments.forPrompt(document.promptId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.linkedDocuments.contentRoot(document.id) })
      queryClient.invalidateQueries({ queryKey: queryKeys.linkedDocuments.versions(document.id) })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    })

    connection.on('LinkedDocumentRemoved', (linkedDocumentId: string, promptId: string) => {
      queryClient.removeQueries({ queryKey: queryKeys.linkedDocuments.detail(linkedDocumentId) })
      queryClient.removeQueries({ queryKey: queryKeys.linkedDocuments.contentRoot(linkedDocumentId) })
      queryClient.removeQueries({ queryKey: queryKeys.linkedDocuments.versions(linkedDocumentId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.linkedDocuments.forPrompt(promptId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    })

    connection.on('TaskWorkflowChanged', (payload: unknown) => {
      const summary = taskSummarySchema.parse(payload)
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.detail(summary.promptId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
    })

    connection.on('AgentUsageUpdated', (payload: unknown) => {
      const usage = agentUsageSchema.parse(payload)
      queryClient.setQueryData(queryKeys.agentUsage.current(), usage)
    })

    connection.on('WorkspaceFileChanged', (payload: unknown) => {
      const change = workspaceFileChangedSchema.parse(payload)
      const expectedKey = fileKey(change.workingDirectoryId, change.relativePath)
      if (change.key !== expectedKey) {
        return
      }

      queryClient.invalidateQueries({
        queryKey: queryKeys.files.content(change.workingDirectoryId, change.relativePath),
      })
      queryClient.invalidateQueries({
        queryKey: queryKeys.files.tree(change.workingDirectoryId, parentDirectoryPath(change.relativePath)),
      })
    })

    connection.onreconnecting(() => setConnected(false))
    connection.onreconnected(() => {
      setConnected(true)
      rejoinAll()
    })
    connection.onclose(() => setConnected(false))

    void connection
      .start()
      .then(() => {
        setConnected(true)
        rejoinAll()
      })
      .catch(() => setConnected(false))

    return () => {
      connectionRef.current = null
      void connection.stop()
    }
  }, [queryClient, rejoinAll])

  useEffect(() => {
    if (!connected) {
      return
    }

    rejoinAll()
  }, [connected, rejoinAll])

  const joinWorkingDirectory = useCallback(
    (id: string) => {
      joinedWorkingDirectoriesRef.current.add(id)
      invokeJoin(id)
    },
    [invokeJoin],
  )

  const leaveWorkingDirectory = useCallback(
    (id: string) => {
      joinedWorkingDirectoriesRef.current.delete(id)
      invokeLeave(id)
    },
    [invokeLeave],
  )

  const joinTasks = useCallback(() => {
    tasksJoinedRef.current = true
    invokeJoinTasks()
  }, [invokeJoinTasks])

  const leaveTasks = useCallback(() => {
    tasksJoinedRef.current = false
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveTasks')
    }
  }, [])

  const joinFile = useCallback(
    (workingDirectoryId: string, relativePath: string) => {
      const key = fileKey(workingDirectoryId, relativePath)
      const existing = joinedFilesRef.current.get(key)
      if (existing) {
        existing.count += 1
        return
      }

      joinedFilesRef.current.set(key, { workingDirectoryId, relativePath, count: 1 })
      invokeJoinFile(workingDirectoryId, relativePath)
    },
    [invokeJoinFile],
  )

  const leaveFile = useCallback(
    (workingDirectoryId: string, relativePath: string) => {
      const key = fileKey(workingDirectoryId, relativePath)
      const existing = joinedFilesRef.current.get(key)
      if (!existing) {
        return
      }

      if (existing.count <= 1) {
        joinedFilesRef.current.delete(key)
        invokeLeaveFile(workingDirectoryId, relativePath)
        return
      }

      existing.count -= 1
    },
    [invokeLeaveFile],
  )

  const value = useMemo(
    () => ({
      connected,
      joinWorkingDirectory,
      leaveWorkingDirectory,
      joinTasks,
      leaveTasks,
      joinFile,
      leaveFile,
    }),
    [connected, joinFile, joinWorkingDirectory, leaveFile, leaveWorkingDirectory, joinTasks, leaveTasks],
  )

  return <PromptHubContext.Provider value={value}>{children}</PromptHubContext.Provider>
}

export function usePromptHub() {
  const context = useContext(PromptHubContext)
  if (!context) {
    throw new Error('usePromptHub must be used within PromptHubProvider')
  }

  return context
}
