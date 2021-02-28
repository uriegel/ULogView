import React, { useState, useEffect, useRef } from 'react'
import { CSSTransition } from 'react-transition-group'
import {ItemsSource, LogView, LogViewItem} from './LogView'
import {Progress} from './Progress'

var requestId = 0

type LogFileItem = {
	id: string
	lineCount: number
}

export type TextPart = {
    text: string
    restrictionIndex: number
}

type LogItem = {
	highlightedText?: TextPart[] 
    text: string
    index?: number
    fileIndex: number
}

type LogItemResult = {
	request: number
	items: LogItem[]
}

function App() {
	const [itemSource, setItemSource] = useState({count: 0, getItems: async (s,e)=>null} as ItemsSource)
	const [id, setId] = useState("")
	const [restricted, setRestricted] = useState(false)
	const [showProgress, setShowProgress] = useState(false)

    const getItem = (text: string, highlightedItems?: TextPart[], index?: number) => ({ 
        item: text, 
		highlightedItems,
        index: index || 0 
    } as LogViewItem)

    const getItems = async (id: string, start: number, end: number) => {
		const data = await fetch(`http://localhost:9865/getitems?id=${id}&req=${++requestId}&start=${start}&end=${end}`)
		const lineItems = (await data.json() as LogItemResult)
		return requestId == lineItems.request
			? lineItems.items.map(n => getItem(n.text, n.highlightedText, n.index))
			: null
	}
	
	useEffect(() => {
		const ws = new WebSocket("ws://localhost:9865/websocketurl")
		ws.onclose = () => console.log("Closed")
		ws.onmessage = p => { 
			const logFileItem = JSON.parse(p.data) as LogFileItem
			setId(logFileItem.id)
			setItemSource({count: logFileItem.lineCount, getItems: (s, e) => getItems(logFileItem.id, s, e) })
		}
	}, [])

    const onKeydown = async (sevt: React.KeyboardEvent) => {
        const evt = sevt.nativeEvent
        if (evt.which == 114) { // F3
			const data = await fetch(`http://localhost:9865/toggleview?id=${id}`)
			const lineItems = (await data.json() as LogItemResult)
			evt.stopImmediatePropagation()
			evt.preventDefault()
			evt.stopPropagation()
			setRestricted(true)
		}
    }

	return (
    	<div className="App" onKeyDown={onKeydown}>
			<LogView itemSource={itemSource} id={id} restricted={restricted}/>
			<CSSTransition
			    in={showProgress}
        		timeout={300}
        		classNames="progress"
        		unmountOnExit
        		// onEnter={() => setShowButton(false)}
        		// onExited={() => setShowButton(true)}
      		>			
				<Progress />
			</CSSTransition>
  		</div>
  	)
}

export default App
