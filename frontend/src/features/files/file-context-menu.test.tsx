import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { FileContextMenu } from './file-context-menu'

describe('FileContextMenu', () => {
  afterEach(() => {
    cleanup()
  })

  it('opens on right-click and fires onShowGitHistory', async () => {
    const user = userEvent.setup()
    const onShowGitHistory = vi.fn()

    render(
      <FileContextMenu onShowGitHistory={onShowGitHistory}>
        <button type="button">file.ts</button>
      </FileContextMenu>,
    )

    fireEvent.contextMenu(screen.getByRole('button', { name: 'file.ts' }))
    expect(screen.getByRole('menuitem', { name: 'Ver historico do git' })).toBeInTheDocument()

    await user.click(screen.getByRole('menuitem', { name: 'Ver historico do git' }))
    expect(onShowGitHistory).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('menuitem', { name: 'Ver historico do git' })).not.toBeInTheDocument()
  })

  it('closes on Escape', async () => {
    const user = userEvent.setup()

    render(
      <FileContextMenu onShowGitHistory={vi.fn()}>
        <button type="button">file.ts</button>
      </FileContextMenu>,
    )

    fireEvent.contextMenu(screen.getByRole('button', { name: 'file.ts' }))
    expect(screen.getByRole('menuitem', { name: 'Ver historico do git' })).toBeInTheDocument()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('menuitem', { name: 'Ver historico do git' })).not.toBeInTheDocument()
  })
})